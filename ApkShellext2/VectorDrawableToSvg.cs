using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml;

namespace ApkQuickReader
{
    /// <summary>
    /// Translates an Android VectorDrawable DOM into SVG markup so the vendored
    /// Svg.NET renderer (already used for .svg icons) draws groups, transforms,
    /// gradients and clip paths instead of the hand-rolled GDI+ walker. Android's
    /// VectorDrawable language is a subset of SVG; this class only maps elements
    /// and resolves color / gradient references ("#AARRGGBB", decimal colors,
    /// "res/x.xml" gradient files, nested vector / selector color indirection).
    /// Anything it cannot map throws — the caller then falls back to the legacy
    /// GDI+ pipeline.
    /// </summary>
    internal static class VectorDrawableToSvg
    {
        sealed class Ctx
        {
            public Func<string, XmlDocument> Load;      // load res/*.xml reference
            public StringBuilder Defs = new StringBuilder();
            public Dictionary<string, string> PaintCache = new Dictionary<string, string>();
            public int NextId;
            public float ViewW, ViewH;
        }

        public static string Translate(XmlElement vector, Func<string, XmlDocument> loadXml)
        {
            if (vector == null || vector.LocalName != "vector") throw new Exception("not a <vector>");
            Ctx ctx = new Ctx { Load = loadXml };
            ctx.ViewW = AttrF(vector, "viewportWidth", 0);
            ctx.ViewH = AttrF(vector, "viewportHeight", 0);
            if (ctx.ViewW <= 0) ctx.ViewW = ctx.ViewH;
            if (ctx.ViewH <= 0) ctx.ViewH = ctx.ViewW;
            if (ctx.ViewW <= 0) throw new Exception("vector without viewport size");

            StringBuilder body = new StringBuilder();
            EmitChildren(body, ctx, vector, 1);
            return "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\""
                + F(ctx.ViewW) + "\" height=\"" + F(ctx.ViewH) + "\" viewBox=\"0 0 "
                + F(ctx.ViewW) + " " + F(ctx.ViewH) + "\">"
                + (ctx.Defs.Length > 0 ? "<defs>" + ctx.Defs + "</defs>" : "")
                + body + "</svg>";
        }

        static void EmitChildren(StringBuilder sb, Ctx ctx, XmlElement parent, int depth)
        {
            // hostile packages can nest <group> thousands deep; without a cap
            // this recursion stack-overflows (uncatchable) and kills explorer
            if (depth > 128) throw new Exception("group nesting too deep");
            // <clip-path> children of <group> clip all siblings of the same group
            string clipId = null;
            foreach (XmlNode n in parent.ChildNodes)
            {
                XmlElement e = n as XmlElement;
                if (e != null && e.LocalName == "clip-path")
                {
                    string d = e.GetAttribute("pathData");
                    if (!string.IsNullOrEmpty(d))
                    {
                        clipId = "c" + (ctx.NextId++);
                        ctx.Defs.Append("<clipPath id=\"").Append(clipId).Append("\"><path d=\"")
                            .Append(d.Replace("&", "&amp;").Replace("<", "&lt;").Replace("\"", "&quot;"))
                            .Append("\"/></clipPath>");
                    }
                }
            }

            foreach (XmlNode n in parent.ChildNodes)
            {
                XmlElement e = n as XmlElement;
                if (e == null) continue;
                string open = clipId != null ? " clip-path=\"url(#" + clipId + ")\"" : "";
                if (e.LocalName == "vector" && !ReferenceEquals(e, parent))
                    continue; // nested <vector> only reachable via color refs, handled in ResolvePaint
                if (e.LocalName == "clip-path")
                    continue; // consumed above
                if (e.LocalName == "group")
                {
                    sb.Append("<g").Append(open);
                    string tr = GroupTransform(e);
                    if (tr.Length > 0) sb.Append(" transform=\"").Append(tr).Append('"');
                    float op = AttrF(e, "opacity", 1f);
                    if (op < 1f) sb.Append(" opacity=\"").Append(F(op)).Append('"');
                    sb.Append('>');
                    EmitChildren(sb, ctx, e, depth + 1);
                    sb.Append("</g>");
                }
                else if (e.LocalName == "path")
                {
                    EmitPath(sb, ctx, e, open);
                }
            }
        }

        static void EmitPath(StringBuilder sb, Ctx ctx, XmlElement e, string clipAttr)
        {
            string d = e.GetAttribute("pathData");
            if (string.IsNullOrEmpty(d)) return;
            double alpha;
            string fill = ResolvePaint(AttrRaw(e, "fillColor", ctx), ctx, 0, out alpha);
            double sa;
            string stroke = ResolvePaint(AttrRaw(e, "strokeColor", ctx), ctx, 0, out sa);
            // aapt:attr inline gradients override the plain attribute
            foreach (XmlNode n in e.ChildNodes)
            {
                XmlElement a = n as XmlElement;
                if (a == null || a.LocalName != "attr") continue;
                string an = a.GetAttribute("name");
                XmlElement grad = null;
                foreach (XmlNode g in a.ChildNodes)
                {
                    XmlElement ge = g as XmlElement;
                    if (ge != null && ge.LocalName == "gradient") { grad = ge; break; }
                }
                if (grad == null) continue;
                string id = EmitGradient(grad, ctx, null);
                if (an == "android:fillColor") { fill = "url(#" + id + ")"; alpha = 1; }
                else if (an == "android:strokeColor") { stroke = "url(#" + id + ")"; sa = 1; }
            }
            double fa = AttrF(e, "fillAlpha", 1f) * alpha;
            double soa = AttrF(e, "strokeAlpha", 1f) * sa;

            sb.Append("<path").Append(clipAttr)
              .Append(" fill=\"").Append(fill ?? "none").Append('"');
            if (fill != null) sb.Append(" fill-opacity=\"").Append(F(fa)).Append('"');
            if (e.GetAttribute("fillType") == "1") sb.Append(" fill-rule=\"evenodd\"");
            if (stroke != null)
            {
                sb.Append(" stroke=\"").Append(stroke).Append('"')
                  .Append(" stroke-width=\"").Append(F(AttrF(e, "strokeWidth", 1f))).Append('"')
                  .Append(" stroke-opacity=\"").Append(F(soa)).Append('"');
                string cap = e.GetAttribute("strokeLineCap");
                if (cap.Length > 0) sb.Append(" stroke-linecap=\"").Append(Esc(cap)).Append('"');
                string join = e.GetAttribute("strokeLineJoin");
                if (join.Length > 0) sb.Append(" stroke-linejoin=\"").Append(Esc(join)).Append('"');
                string mit = e.GetAttribute("strokeMiterLimit");
                if (mit.Length > 0) sb.Append(" stroke-miterlimit=\"").Append(Esc(mit)).Append('"');
            }
            else sb.Append(" stroke=\"none\"");
            sb.Append(" d=\"").Append(Esc(d)).Append("\"/>");
        }

        static string Esc(string s)
        {
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace("\"", "&quot;");
        }

        /// <summary>Android order: scale about pivot, rotate about pivot, translate.</summary>
        static string GroupTransform(XmlElement g)
        {
            float tx = AttrF(g, "translateX", 0), ty = AttrF(g, "translateY", 0);
            float sx = AttrF(g, "scaleX", 1), sy = AttrF(g, "scaleY", 1);
            float rot = AttrF(g, "rotation", 0);
            float px = AttrF(g, "pivotX", 0), py = AttrF(g, "pivotY", 0);
            StringBuilder t = new StringBuilder();
            if (tx != 0 || ty != 0) t.Append("translate(").Append(F(tx)).Append(',').Append(F(ty)).Append(") ");
            if (rot != 0) t.Append("rotate(").Append(F(rot)).Append(',').Append(F(px)).Append(',').Append(F(py)).Append(") ");
            if (sx != 1 || sy != 1)
            {
                if (px != 0 || py != 0) t.Append("translate(").Append(F(px)).Append(',').Append(F(py)).Append(") ");
                t.Append("scale(").Append(F(sx)).Append(',').Append(F(sy)).Append(")");
                if (px != 0 || py != 0) t.Append(" translate(").Append(F(-px)).Append(',').Append(F(-py)).Append(")");
            }
            return t.ToString().TrimEnd();
        }

        /// <summary>Resolve an Android paint value to an SVG paint string; null = none.</summary>
        static string ResolvePaint(string raw, Ctx ctx, int depth, out double alpha)
        {
            alpha = 1;
            if (string.IsNullOrEmpty(raw)) return null;
            if (raw.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                string cached;
                if (ctx.PaintCache.TryGetValue(raw, out cached)) return cached;
                XmlDocument doc = null;
                if (ctx.Load != null) try { doc = ctx.Load(raw); } catch { doc = null; }
                if (doc == null || doc.DocumentElement == null) throw new Exception("cannot load paint " + raw);
                XmlElement root = doc.DocumentElement;
                string result;
                if (root.LocalName == "gradient")
                    result = "url(#" + EmitGradient(root, ctx, raw) + ")";
                else if (depth < 4)
                    result = ResolvePaint(IndirectColor(root, ctx), ctx, depth + 1, out alpha);
                else throw new Exception("paint reference chain too deep: " + raw);
                ctx.PaintCache[raw] = result;
                return result;
            }
            System.Drawing.Color c = ParseColor(raw);
            alpha = c.A / 255.0;
            return string.Format(CultureInfo.InvariantCulture, "#{0:x2}{1:x2}{2:x2}", c.R, c.G, c.B);
        }

        /// <summary>color of a selector item / vector first path / shape solid / <color>.</summary>
        static string IndirectColor(XmlElement root, Ctx ctx)
        {
            if (root.LocalName == "selector")
            {
                XmlElement item = root.SelectSingleNode("item") as XmlElement;
                if (item == null) item = root;
                string c = item.GetAttribute("color");
                if (!string.IsNullOrEmpty(c)) return c;
            }
            else if (root.LocalName == "vector")
            {
                foreach (XmlNode n in root.SelectNodes(".//path"))
                {
                    XmlElement p = n as XmlElement;
                    string c = p != null ? p.GetAttribute("fillColor") : null;
                    if (!string.IsNullOrEmpty(c)) return c;
                }
            }
            else if (root.LocalName == "shape")
            {
                XmlElement solid = root.SelectSingleNode("solid") as XmlElement;
                if (solid != null) return solid.GetAttribute("color");
            }
            else if (root.LocalName == "color")
            {
                return root.HasAttribute("color") ? root.GetAttribute("color") : root.InnerText.Trim();
            }
            throw new Exception("no resolvable color in <" + root.LocalName + ">");
        }

        static string EmitGradient(XmlElement grad, Ctx ctx, string cacheKey)
        {
            string id = "g" + (ctx.NextId++);
            string type = grad.GetAttribute("type"); // "0"=linear "1"=radial "2"=sweep (or names)
            bool radial = type == "1" || type == "radial";
            bool sweep = type == "2" || type == "sweep";

            List<double> offs = new List<double>();
            List<System.Drawing.Color> cols = new List<System.Drawing.Color>();
            foreach (XmlNode n in grad.ChildNodes)
            {
                XmlElement item = n as XmlElement;
                if (item == null || item.LocalName != "item") continue;
                string col = item.GetAttribute("color");
                if (string.IsNullOrEmpty(col)) continue;
                System.Drawing.Color c = ParseColor(col);
                double o;
                if (!double.TryParse(item.GetAttribute("offset"), NumberStyles.Float, CultureInfo.InvariantCulture, out o))
                    o = cols.Count; // fall back to index
                offs.Add(Math.Max(0, Math.Min(1, o)));
                cols.Add(c);
            }
            if (cols.Count == 0)
            {
                string sc = grad.GetAttribute("startColor"), ec = grad.GetAttribute("endColor"), cc = grad.GetAttribute("centerColor");
                if (string.IsNullOrEmpty(sc) || string.IsNullOrEmpty(ec)) throw new Exception("gradient without colors");
                cols.Add(ParseColor(sc)); offs.Add(0);
                if (!string.IsNullOrEmpty(cc)) { cols.Add(ParseColor(cc)); offs.Add(radial ? 0.5 : 0.5); }
                cols.Add(ParseColor(ec)); offs.Add(1);
            }
            // stable insertion sort by offset
            for (int i = 1; i < offs.Count; i++)
                for (int j = i; j > 0 && offs[j] < offs[j - 1]; j--)
                { double t = offs[j]; offs[j] = offs[j - 1]; offs[j - 1] = t; System.Drawing.Color tc = cols[j]; cols[j] = cols[j - 1]; cols[j - 1] = tc; }

            StringBuilder stops = new StringBuilder();
            for (int i = 0; i < cols.Count; i++)
                stops.Append("<stop offset=\"").Append(F(offs[i])).Append("\" stop-color=\"")
                     .Append(string.Format(CultureInfo.InvariantCulture, "#{0:x2}{1:x2}{2:x2}", cols[i].R, cols[i].G, cols[i].B))
                     .Append("\" stop-opacity=\"").Append(F(cols[i].A / 255.0)).Append("\"/>");

            StringBuilder gtag = new StringBuilder();
            if (radial)
            {
                float cx = AttrF(grad, "centerX", ctx.ViewW / 2), cy = AttrF(grad, "centerY", ctx.ViewH / 2);
                float r = AttrF(grad, "gradientRadius", Math.Min(ctx.ViewW, ctx.ViewH) / 2);
                if (r <= 0) r = Math.Min(ctx.ViewW, ctx.ViewH) / 2;
                gtag.Append("<radialGradient id=\"").Append(id).Append("\" gradientUnits=\"userSpaceOnUse\" cx=\"")
                    .Append(F(cx)).Append("\" cy=\"").Append(F(cy)).Append("\" r=\"").Append(F(r)).Append("\">");
            }
            else
            {
                // sweep has no SVG 1.1 equivalent: linear approximation along x.
                float x1, y1, x2, y2;
                bool explicitPts = grad.HasAttribute("startX") && grad.HasAttribute("endX");
                if (explicitPts)
                {
                    x1 = AttrF(grad, "startX", 0); y1 = AttrF(grad, "startY", 0);
                    x2 = AttrF(grad, "endX", ctx.ViewW); y2 = AttrF(grad, "endY", 0);
                }
                else
                {
                    double ang = AttrF(grad, "angle", 0) * Math.PI / 180.0;
                    double dx = Math.Cos(ang), dy = Math.Sin(ang);
                    double len = Math.Abs(ctx.ViewW * dx) + Math.Abs(ctx.ViewH * dy);
                    double cx = ctx.ViewW / 2.0, cy = ctx.ViewH / 2.0;
                    x1 = (float)(cx - dx * len / 2); y1 = (float)(cy - dy * len / 2);
                    x2 = (float)(cx + dx * len / 2); y2 = (float)(cy + dy * len / 2);
                }
                gtag.Append("<linearGradient id=\"").Append(id).Append("\" gradientUnits=\"userSpaceOnUse\" x1=\"")
                    .Append(F(x1)).Append("\" y1=\"").Append(F(y1)).Append("\" x2=\"").Append(F(x2))
                    .Append("\" y2=\"").Append(F(y2)).Append("\">");
            }
            gtag.Append(stops).Append(radial ? "</radialGradient>" : "</linearGradient>");
            ctx.Defs.Append(gtag);
            return id;
        }

        /// <summary>
        /// Framework colors (package 0x0106) live in android.jar, not in the APK, so
        /// the resource decoder falls back to the raw-id form "(0x0106000b)". The
        /// public android.R.color ids are frozen constants (verified against AOSP
        /// core/res/res/values/public.xml + Mono.Android bindings); the entries with
        /// literal hex values in the platform color resources are tabled here so a
        /// vector fill like @android:color/white renders instead of throwing.
        /// Unknown ids keep throwing so callers fall back instead of guessing.
        /// </summary>
        internal static class FrameworkColors
        {
            static readonly Dictionary<uint, uint> IdToArgb = new Dictionary<uint, uint>
            {
                { 0x0106000b, 0xFFFFFFFF }, // white
                { 0x0106000c, 0xFF000000 }, // black
                { 0x0106000d, 0x00000000 }, // transparent
                { 0x0106000e, 0xFF000000 }, // background_dark
                { 0x0106000f, 0xFFFFFFFF }, // background_light
                { 0x01060012, 0xFF33B5E5 }, // holo_blue_light
                { 0x01060013, 0xFF0099CC }, // holo_blue_dark
                { 0x01060014, 0xFF99CC00 }, // holo_green_light
                { 0x01060015, 0xFF669900 }, // holo_green_dark
                { 0x01060016, 0xFFFF4444 }, // holo_red_light
                { 0x01060017, 0xFFCC0000 }, // holo_red_dark
                { 0x01060018, 0xFFFFBB33 }, // holo_orange_light
                { 0x01060019, 0xFFFF8800 }, // holo_orange_dark
                { 0x0106001A, 0xFFAA66CC }, // holo_purple
                { 0x0106001B, 0xFF00DDFF }, // holo_blue_bright
            };

            public static bool TryResolve(string raw, out System.Drawing.Color color)
            {
                color = System.Drawing.Color.Empty;
                if (raw == null) return false;
                string v = raw.Trim();
                if (v.Length > 2 && v[0] == '(' && v[v.Length - 1] == ')')
                    v = v.Substring(1, v.Length - 2);
                if (!v.StartsWith("0x") && !v.StartsWith("0X")) return false;
                uint id;
                if (!uint.TryParse(v.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id))
                    return false;
                uint argb;
                if (!IdToArgb.TryGetValue(id, out argb)) return false;
                color = System.Drawing.Color.FromArgb((int)argb);
                return true;
            }
        }

        static System.Drawing.Color ParseColor(string v)
        {
            v = v.Trim();
            System.Drawing.Color fw;
            if (FrameworkColors.TryResolve(v, out fw)) return fw;
            if (v.StartsWith("#"))
            {
                if (v.Length == 9)
                    return System.Drawing.Color.FromArgb(
                        Convert.ToInt32(v.Substring(1, 2), 16), Convert.ToInt32(v.Substring(3, 2), 16),
                        Convert.ToInt32(v.Substring(5, 2), 16), Convert.ToInt32(v.Substring(7, 2), 16));
                if (v.Length == 7)
                    return System.Drawing.Color.FromArgb(
                        Convert.ToInt32(v.Substring(1, 2), 16), Convert.ToInt32(v.Substring(3, 2), 16),
                        Convert.ToInt32(v.Substring(5, 2), 16));
                if (v.Length == 5)
                    return System.Drawing.Color.FromArgb(
                        Convert.ToInt32(new string(v[1], 2), 16), Convert.ToInt32(new string(v[2], 2), 16),
                        Convert.ToInt32(new string(v[3], 2), 16));
                throw new Exception("bad color " + v);
            }
            uint d;
            if (uint.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out d))
                return System.Drawing.Color.FromArgb((int)d);
            throw new Exception("bad color " + v);
        }

        static string AttrRaw(XmlElement e, string name, Ctx ctx)
        {
            return e.HasAttribute(name) ? e.GetAttribute(name) : null;
        }

        static float AttrF(XmlElement e, string name, float def)
        {
            if (!e.HasAttribute(name)) return def;
            float v;
            if (float.TryParse(e.GetAttribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out v)
                && !float.IsNaN(v) && !float.IsInfinity(v)) return v;
            return def;
        }

        static string F(double v) { return v.ToString("0.####", CultureInfo.InvariantCulture); }
    }
}
