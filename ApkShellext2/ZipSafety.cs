using System;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;

namespace ApkQuickReader {
    /// <summary>
    /// Every package reader here opens attacker-controlled ZIP archives from
    /// inside explorer.exe (icon/infotip/context-menu handlers). Declared entry
    /// sizes in the central directory are attacker data: never use them for
    /// (int) casts or buffer preallocation, and always bound total bytes read.
    /// ReadEntryBounded is the single sanctioned way to materialise an entry.
    /// (Ratio doctrine borrowed from apk-info, Apache-2.0: worst-case deflate
    /// expansion is a 258-byte match for ~2 bits ≈ 1032×.)
    /// </summary>
    internal static class ZipSafety {
        // generous per-role caps — real payloads are far below these
        public const long MAX_BASE_APK = 512L * 1024 * 1024;
        public const long MAX_MANIFEST = 128L * 1024 * 1024;
        public const long MAX_XML = 64L * 1024 * 1024;
        public const long MAX_ICON = 32L * 1024 * 1024;
        public const long MAX_METADATA = 8L * 1024 * 1024;   // plist / json / itunes
        public const long RATIO_LIMIT = 1032;

        /// <summary>
        /// Reads an entry with a hard byte cap and a compression-ratio cap.
        /// The declared Size/CompressedSize are never trusted and never used
        /// for preallocation; the buffer grows only as real bytes arrive.
        /// Throws InvalidDataException when a cap is hit — handlers catch and
        /// fall back to the generic icon.
        /// </summary>
        public static byte[] ReadEntryBounded(ZipFile z, ZipEntry entry, long hardCap, string what) {
            if (entry == null)
                throw new InvalidDataException(what + ": entry missing");
            long ratioCap = entry.CompressionMethod == CompressionMethod.Stored
                ? hardCap
                : checked(entry.CompressedSize * RATIO_LIMIT + (1L << 20));
            long limit = Math.Min(hardCap, ratioCap);
            // start small, grow with real data — never preallocate declared size
            int pre = 64 * 1024;
            if (entry.Size > 0 && entry.Size < pre) pre = (int)entry.Size;
            MemoryStream ms = new MemoryStream(pre);
            byte[] buf = new byte[64 * 1024];
            using (Stream s = z.GetInputStream(entry)) {
                long total = 0; int n;
                while ((n = s.Read(buf, 0, buf.Length)) > 0) {
                    total += n;
                    if (total > limit)
                        throw new InvalidDataException(what + ": decompressed size exceeds safety cap " + limit);
                    ms.Write(buf, 0, n);
                }
            }
            return ms.ToArray();
        }

        /// <summary>
        /// Bounds a declared sub-allocation (e.g. a PNG chunk length field read
        /// from a ≤ MAX_ICON entry) against the remaining bytes in its source
        /// buffer — declared length must fit inside what we actually read.
        /// </summary>
        public static long ClampDeclared(long declared, long remaining, long hardCap, string what) {
            if (declared < 0 || declared > hardCap || declared > remaining)
                throw new InvalidDataException(what + ": declared length " + declared + " out of bounds");
            return declared;
        }
    }
}
