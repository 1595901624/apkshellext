using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ApkShellext2;
using ICSharpCode.SharpZipLib.Zip;

namespace ApkQuickReader {
    /// <summary>
    /// Reader for multi-split Android package containers: .apks (bundletool),
    /// .apkm (APKMirror) and .xapk (APKPure). All three are plain ZIP archives
    /// holding several .apk files plus container metadata. The application's
    /// identity (manifest/resources/icon) lives in the BASE apk; split_config.*
    /// entries only add per-density/language resources on top of it.
    /// Base selection order mirrors apk-info (Apache-2.0) base_apk_candidates:
    ///   xapk manifest.json split with id=="base"  >  literal "base.apk"  >
    ///   bundletool toc.pb well-known paths  >  root-level *.apk guess
    /// (candidates whose manifest carries android:split are rejected).
    /// .apkm/.xapk usually also ship a ready-made icon.png at the archive root,
    /// which is preferred for icon display (zero parse cost).
    /// </summary>
    public class SplitApkBundleReader : AppPackageReader {
        private FileStream outerStream;
        private ZipFile outer;
        private AppPackageReader inner;
        private string baseEntryName;

        // bundletool mode paths, splits/ before instant/ on purpose
        private static readonly string[] tocBasePaths = {
            "splits/base-master.apk", "universal.apk", "system/system.apk",
            "archive/archive.apk", "instant/instant-base-master.apk",
        };

        // caps + ratio doctrine centralised in ZipSafety (shared by all readers)
        private const long MAX_JSON_BYTES = 2L * 1024 * 1024;

        public SplitApkBundleReader(string filename) {
            FileName = filename;
            outerStream = new FileStream(filename, FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.Delete);
            outer = new ZipFile(outerStream);

            baseEntryName = selectBaseEntry();
            if (baseEntryName == null)
                throw new NotSupportedException(filename + " contains no base apk (encrypted apkm or unsupported layout?)");
            // the base apk itself is only inflated on demand (EnsureInner):
            // icon and infotip usually resolve from the root icon.png alone,
            // which costs a few KB instead of decompressing up to 512 MB
        }

        private void EnsureInner() {
            if (inner != null) return;
            ZipEntry baseEntry = outer.GetEntry(baseEntryName);
            // The nested apk must be seekable for ZipFile; SZL's decompression
            // streams are not, so buffer the whole entry — under hard caps so a
            // zip bomb cannot balloon explorer. (STORED-window zero-copy is a
            // possible later optimisation; correctness and safety first.)
            byte[] baseBytes = ZipSafety.ReadEntryBounded(outer, baseEntry, ZipSafety.MAX_BASE_APK, "base apk " + baseEntryName);
            MemoryStream ms = new MemoryStream(baseBytes, 0, baseBytes.Length, false, false);
            AppPackageReader candidate = new ApkReader(ms);
            // wrong-base guard: a split has android:split in its manifest
            if (!string.IsNullOrEmpty(((ApkReader)candidate).getAttribute("manifest", "split"))) {
                candidate.Dispose();
                Log("selected base " + baseEntryName + " looks like a split, rejecting");
                throw new NotSupportedException("base candidate " + baseEntryName + " is a split apk");
            }
            inner = candidate;
        }

        private string selectBaseEntry() {
            // 1. xapk manifest.json: {"split_apks":[{"file":"...","id":"base"}, ...]}
            ZipEntry mf = outer.GetEntry("manifest.json");
            if (mf != null) {
                string json = ReadEntryText(mf);
                // tolerant to BOM/whitespace; pair file/id inside each flat object
                foreach (Match block in Regex.Matches(json, @"\{[^{}]*\}")) {
                    Match id = Regex.Match(block.Value, @"""id""\s*:\s*""([^""]*)""");
                    Match file = Regex.Match(block.Value, @"""file""\s*:\s*""([^""]*)""");
                    if (id.Success && file.Success &&
                        string.Equals(id.Groups[1].Value, "base", StringComparison.OrdinalIgnoreCase)) {
                        string cand = file.Groups[1].Value.Replace('\\', '/');
                        if (IsValidEntryName(cand)) return cand;
                        Log("manifest.json base name rejected: " + cand);
                    }
                }
                // alternate xapk schema: {"name":"pkg","split_apks":[{"apkFile":"..."}]}
                Match alt = Regex.Match(json, @"""apkFile""\s*:\s*""([^""]*base[^""]*\.apk)""", RegexOptions.IgnoreCase);
                if (alt.Success) {
                    string cand = alt.Groups[1].Value.Replace('\\', '/');
                    if (IsValidEntryName(cand)) return cand;
                    Log("manifest.json base name rejected: " + cand);
                }
            }

            // 2. APKMirror / most tooling: literal base.apk at root
            if (outer.GetEntry("base.apk") != null) return "base.apk";

            // 3. bundletool toc.pb container: try the well-known base paths
            if (outer.GetEntry("toc.pb") != null) {
                foreach (string p in tocBasePaths)
                    if (outer.GetEntry(p) != null) return p;
                Match standalone = null; // standalones/standalone<N>.apk, lowest N
                int bestN = int.MaxValue;
                foreach (ZipEntry e in outer) {
                    if (!e.IsFile) continue;
                    Match m = Regex.Match(e.Name, @"standalones/standalone(\d{1,6})\.apk$", RegexOptions.IgnoreCase);
                    int n;
                    if (m.Success && int.TryParse(m.Groups[1].Value, out n) && n < bestN) {
                        bestN = n;
                        standalone = m;
                    }
                }
                if (standalone != null) return standalone.Value;
            }

            // 4. guess: root-level *.apk that is not a config/split companion; largest wins
            ZipEntry best = null;
            foreach (ZipEntry e in outer) {
                if (!e.IsFile) continue;
                string n = e.Name;
                if (!n.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) continue;
                if (n.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase)) continue;
                string leaf = Path.GetFileName(n);
                if (leaf.IndexOf("config", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    leaf.StartsWith("split", StringComparison.OrdinalIgnoreCase)) continue;
                int depth = n.Count(c => c == '/');
                int score = (depth == 0 ? 1 << 30 : 0) + (int)(e.Size >> 20);
                int bestScore = best == null ? -1
                    : (best.Name.Count(c => c == '/') == 0 ? 1 << 30 : 0) + (int)(best.Size >> 20);
                if (score > bestScore) best = e;
            }
            return best == null ? null : best.Name;
        }

        // names come from an attacker-controlled manifest.json; we only ever use
        // them as zip lookups (never filesystem paths) but keep them clean anyway
        private static bool IsValidEntryName(string name) {
            if (string.IsNullOrEmpty(name) || name.Length > 260) return false;
            if (name.IndexOf(':') >= 0) return false;               // drive letter / NTFS stream
            if (name.StartsWith("/") || name.StartsWith("\\")) return false;
            foreach (string seg in name.Split('/'))
                if (seg == "..") return false;
            return true;
        }

        private string ReadEntryText(ZipEntry entry) {
            byte[] b = ZipSafety.ReadEntryBounded(outer, entry, MAX_JSON_BYTES, "metadata " + entry.Name);
            int off = 0;
            if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) off = 3; // BOM
            return Encoding.UTF8.GetString(b, off, b.Length - off);
        }

        public override AppPackageReader.AppType Type { get { return AppType.AndroidApp; } }
        public override string AppName { get { EnsureInner(); return inner.AppName; } }
        public override string PackageName { get { EnsureInner(); return inner.PackageName; } }
        public override string Version { get { EnsureInner(); return inner.Version; } }
        public override string Revision { get { EnsureInner(); return inner.Revision; } }
        public override string Publisher { get { EnsureInner(); return inner.Publisher; } }
        public override string AppID { get { EnsureInner(); return inner.AppID; } }
        public override CultureInfo Culture {
            get { EnsureInner(); return inner.Culture; }
            set { EnsureInner(); inner.Culture = value; }
        }

        // Root-level icon.png shipped by APKMirror/APKPure — the cheap, high-quality path.
        public override Bitmap Icon {
            get {
                Bitmap b = readRootIcon();
                if (b != null) return b;
                EnsureInner();
                return inner.Icon;
            }
        }

        public override Bitmap getIcon(Size size) {
            Bitmap b = readRootIcon();
            if (b == null) {
                EnsureInner();
                return inner.getIcon(size);
            }
            using (b) return Utility.ResizeBitmap(b, size);
        }

        private Bitmap readRootIcon() {
            ZipEntry best = null;
            foreach (ZipEntry e in outer) {
                if (!e.IsFile) continue;
                string leaf = e.Name;
                int slash = leaf.LastIndexOf('/');
                if (slash >= 0) continue; // root only
                if (!leaf.StartsWith("icon", StringComparison.OrdinalIgnoreCase)) continue;
                if (!leaf.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
                if (best == null || e.Size > best.Size) best = e; // prefer largest variant
            }
            if (best == null) return null;
            try {
                byte[] raw = ZipSafety.ReadEntryBounded(outer, best, ZipSafety.MAX_ICON, "root icon " + best.Name);
                using (MemoryStream ms = new MemoryStream(raw, 0, raw.Length, false, false)) {
                    // full buffer before FromStream: GDI+ lazy decode + non-seekable zip stream
                    return new Bitmap(ms, true);
                }
            } catch (Exception e) {
                Log("root icon.png decode failed: " + e.Message);
                return null;
            }
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                if (inner != null) { inner.Dispose(); inner = null; }
                if (outer != null) { outer.Close(); outer = null; }
                if (outerStream != null) { outerStream.Dispose(); outerStream = null; }
            }
            base.Dispose(disposing);
        }
    }
}
