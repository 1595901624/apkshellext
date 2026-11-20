using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace ApkShellext2 {
    /// <summary>Captures Trace/Debug output (Svg.NET swallows render exceptions into Trace).</summary>
    internal class TraceToFileListener : DefaultTraceListener {
        public override void WriteLine(string message) { DbgLog.W("TRACE " + message); }
        public override void Write(string message) { DbgLog.W("TRACE " + message); }
    }

    /// <summary>
    /// File diagnostic log for debugging handler runs inside host processes
    /// (explorer.exe) where Trace/EventLog output is not visible.
    /// Enabled by registry: HKCU\Software\ApkShellext2 /v DebugLog /t REG_SZ /d True
    /// Log file: %PUBLIC%\apkshellext-dbg.log
    /// </summary>
    internal static class DbgLog {
        private static readonly object _lock = new object();
        private static readonly string _path =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                         "apkshellext-dbg.log");
        private static readonly bool? _enabled = Init();
        private static readonly string _proc;

        static DbgLog() {
            try { _proc = Process.GetCurrentProcess().ProcessName; }
            catch { _proc = "?"; }
            if (_enabled == true) {
#if DEBUG
                // Hijacking the process-wide Trace/Debug listeners is a DEBUG-only diagnostic:
                // a release shell extension must not rewrite the host's (explorer's) listeners.
                // Debug.Listeners additionally does not exist on CoreCLR — referencing it
                // statically threw MissingMethodException from this cctor in pwsh7 harness runs.
                try {
                    System.Diagnostics.Trace.Listeners.Clear();
                    System.Diagnostics.Trace.Listeners.Add(new TraceToFileListener());
                    var dbgL = typeof(System.Diagnostics.Debug).GetProperty("Listeners",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (dbgL != null) {
                        var coll = (TraceListenerCollection)dbgL.GetValue(null);
                        coll.Clear();
                        coll.Add(new TraceToFileListener());
                    }
                    W("TRACE listeners attached");
                } catch { }
#endif
                try {
                    var st = typeof(Svg.SvgDocument);
                    var an = st.Assembly;
                    W("ENV svgasm=" + an.FullName + " loc=" + SafeLoc(an) + " clr=" + Environment.Version +
                      " cu=" + System.Globalization.CultureInfo.CurrentCulture.Name +
                      " cui=" + System.Globalization.CultureInfo.CurrentUICulture.Name);
                    LogConv("SvgTransformCollection", st.Assembly.GetType("Svg.SvgTransformCollection"));
                    LogConv("SvgUnit", st.Assembly.GetType("Svg.SvgUnit"));
                    LogConv("SvgViewBox", st.Assembly.GetType("Svg.DataTypes.SvgViewBox"));
                } catch (Exception ex) { W("ENV FAIL " + ex); }
            }
        }

        private static string SafeLoc(System.Reflection.Assembly a) {
            try { return a.IsDynamic ? "(dynamic)" : a.Location; } catch { return "(n/a)"; }
        }

        private static void LogConv(string label, Type t) {
            if (t == null) { W("ENV conv " + label + ": TYPE NOT FOUND"); return; }
            try {
                var c = System.ComponentModel.TypeDescriptor.GetConverter(t);
                W("ENV conv " + label + " -> " + c.GetType().FullName + " canFromStr=" + c.CanConvertFrom(typeof(string)));
            } catch (Exception ex) { W("ENV conv " + label + " EX " + ex.GetType().Name + ": " + ex.Message); }
        }

        private static bool? Init() {
            try {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\ApkShellext2")) {
                    if (key != null && string.Equals(key.GetValue("DebugLog") as string, "True",
                        StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            } catch { }
            return false;
        }

        public static bool Enabled { get { return _enabled == true; } }

        public static void W(string msg) {
            if (_enabled != true) return;
            try {
                string line = DateTime.Now.ToString("HH:mm:ss.fff") + " [" + _proc + "/" +
                    Thread.CurrentThread.ManagedThreadId + " clr" + Environment.Version + "] " + msg + "\n";
                lock (_lock) File.AppendAllText(_path, line);
            } catch { }
        }

        private static int _seq;

        /// <summary>Save an intermediate text artifact (translated SVG) for host-vs-harness diffing.</summary>
        public static void DumpText(string text, string tag) {
            if (_enabled != true || string.IsNullOrEmpty(text)) return;
            try {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "apkshellext-dbg");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                int n = Interlocked.Increment(ref _seq);
                string safe = tag.Replace('\\', '_').Replace('/', '_');
                string p = Path.Combine(dir, DateTime.Now.ToString("HHmmss") + "_" + n.ToString("D3") + "_" + _proc + "_" + safe);
                File.WriteAllText(p, text);
                W("DUMPTEXT " + safe + " -> " + p);
            } catch { }
        }

        /// <summary>Save the exact bitmap a handler returns, so out-of-proc renders can be inspected.</summary>
        public static void Dump(System.Drawing.Bitmap b, string tag) {
            if (_enabled != true || b == null) return;
            try {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "apkshellext-dbg");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string safe = tag.Replace('\\', '_').Replace('/', '_');
                if (safe.Length > 40) safe = safe.Substring(safe.Length - 40);
                string p = Path.Combine(dir, DateTime.Now.ToString("HHmmss_fff") + "_" + _proc + "_" + safe + "_" + b.Width + ".png");
                b.Save(p, System.Drawing.Imaging.ImageFormat.Png);
                W("DUMP " + safe + " -> " + p);
            } catch { }
        }

        /// <summary>Compact 4x4 ARGB corner/center sample of a bitmap for pixel forensics.</summary>
        public static string Sample(System.Drawing.Bitmap b) {
            if (b == null) return "null";
            try {
                int[] xs = { 2, b.Width / 4, b.Width / 2, b.Width - 3 };
                int[] ys = { 2, b.Height / 4, b.Height / 2, b.Height - 3 };
                var sb = new System.Text.StringBuilder(b.Width + "x" + b.Height + ":");
                foreach (int y in ys) foreach (int x in xs) {
                    var c = b.GetPixel(x, y);
                    sb.Append(' ').Append(c.A.ToString("X2")).Append(c.R.ToString("X2"))
                      .Append(c.G.ToString("X2")).Append(c.B.ToString("X2"));
                }
                return sb.ToString();
            } catch (Exception ex) { return "sample-fail " + ex.Message; }
        }
    }
}
