using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using System.IO;
using ApkQuickReader;
using ApkShellext2.Properties;
using System.Globalization;
using System.Xml;
using System.Resources;
using System.Drawing;
using System.Drawing.Drawing2D;
using WebPWrapper;

namespace ApkShellext2
{
    /// <summary>
    /// A Android App Bundler Reader
    /// Not implemented yet
    /// </summary>
    public class AabReader: AppPackageReader
    {
        private ZipFile zip;
        private byte[] manifest;

        private const string AndroidManifestXML = @"base/manifest/androidmanifest.xml";
        private const string TagApplication = @"manifest/application";
        private const string TagManifest = @"manifest";
        private const string AttrLabel = @"label";
        private const string AttrVersionName = @"versionName";
        private const string AttrVersionCode = @"versionCode";
        private const string AttrIcon = @"icon";
        private const string AttrPackage = @"package";

        private const string ConstTrue = "true";
        private const string ConstFalse = "false";

        public AabReader(string path) 
        {
            Log("create AabReader new");
            FileName = path;
            // FileShare.Delete so Explorer can delete/rename the package while handlers are alive (kkguo/apkshellext#103)
            openStream(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete));
        }


        private void openStream(Stream stream)
        {
            zip = new ZipFile(stream);
            ZipEntry en = zip.GetEntry(AndroidManifestXML);
            Log("find manifest: " + (en == null).ToString());
            // bounded read: attacker-declared entry sizes must never drive
            // (int) casts or buffer allocation in explorer's process
            manifest = ZipSafety.ReadEntryBounded(zip, en, ZipSafety.MAX_MANIFEST, "AndroidManifest.xml");
        }

        public override AppPackageReader.AppType Type
        {
            get
            {
                return AppType.AndroidAab;
            }
        }

        public override string AppName
        {
            get
            {
                return getAttribute(TagApplication, AttrLabel);
            }
        }
        public override string Version
        {
            get
            {
                return getAttribute(TagManifest, AttrVersionName);
            }
        }
        public override string Revision
        {
            get
            {
                return getAttribute(TagManifest, AttrVersionCode);
            }
        }

        public override Bitmap Icon => getImage(TagApplication, AttrIcon, new Size(64, 64));

        public override string PackageName => getAttribute(TagManifest, AttrPackage);

        // get an icon with size
        public override Bitmap getIcon(Size size)
        {
            Bitmap icon = Utility.AppTypeIcon(AppPackageReader.AppType.AndroidApp);
            return icon;
            //return getImage(TagApplication, AttrIcon, size);
        }
        /// <summary>
        /// Get a Image object from manifest and resources
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="attr"></param>
        /// <returns></returns>
        public Bitmap getImage(string tag, string attr, Size size)
        {
            Log(tag+ "..." + attr + "..." + size.ToString());
            //if (getFlag("Density") == null)
            //{
            //    setFlag("Density", 1);
            //}
            //string path = QuickSearchManifestXml(tag, attr);
            //if (path == "")
            //{
            //    Log("Cannot find image for <" + tag + ">.<" + attr + ">");
            //    throw new Exception("Cannot find image for <" + tag + ">.<" + attr + ">");
            //}
            string path = "ic_launcher.png";
            try
            {
                if (path != "")
                {
                    return getImage(path, size);
                }
                throw new Exception("Cannot found image " + tag + "@" + attr);
            }
            catch (Exception ex)
            {
                Log("Image path : " + path);
                Log("Error happens during extracting image, " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// return the bitmap of a image file path located in apk zip
        /// accepting .png, adaptive-icon or vectordrawable
        /// </summary>
        /// <param name="path">png or xml file within apk zip</param>
        /// <returns>Bitmap or </returns>
        public Bitmap getImage(string path, Size size)
        {
            ZipEntry iconz;
            if (zip.FindEntry(path, true) >= 0)
            {
                iconz = ZipSafety.GetEntryExact(zip, path);
                // case-insensitive extension dispatch, see kkguo/apkshellext#99
                string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" ||
                    ext == ".gif" || ext == ".tif" || ext == ".tiff")
                {
                    // fully buffer before decoding: GDI+ decodes PNG lazily and renders BLANK pixels when
                    // scaled from SharpZipLib 1.4.2's non-seekable zip streams after they are closed
                    byte[] imgBytes = ZipSafety.ReadEntryBounded(zip, iconz, ZipSafety.MAX_ICON, "image " + path);
                    using (MemoryStream ms = new MemoryStream(imgBytes, 0, imgBytes.Length, false, false))
                        return Utility.ResizeBitmap((Bitmap)Image.FromStream(ms), size);
                }
                else if (ext == ".webp")
                {
                    WebP webp = new WebP();
                    byte[] bytes = ZipSafety.ReadEntryBounded(zip, iconz, ZipSafety.MAX_ICON, "webp " + path);
                    //webp.Decode(bytes).Save(FileName+".webp");
                    return Utility.ResizeBitmap(webp.Decode(bytes), size);
                }
                else if (ext == ".xml")
                {
                    XmlDocument doc = ExtractCompressedXml(path);
                    if (doc.FirstChild.Name == "adaptive-icon")
                    {
                        return parseAdaptiveIcon(doc, size);
                    }
                    else if (doc.FirstChild.Name == "vector")
                    { // this is a vectordrawable
                        return parseVectorDrawable(doc, size);
                    }
                    else if (doc.FirstChild.Name == "shape")
                    {
                        return parseShape(doc, size);
                    }
                    else if (doc.FirstChild.Name == "layer-list")
                    {
                        return parseLayerDrawable(doc, size);
                    }
                    else
                    {
                        throw new Exception("unsupported image file " + path + " with tag " + doc.FirstChild.Name);
                    }
                }
            }
            return null;
        }

        private Bitmap parseLayerDrawable(XmlNode node, Size size)
        {
            try
            {
                XmlNodeList nl = node.SelectNodes("/layer-list/item");
                List<Bitmap> bitmapList = new List<Bitmap>(nl.Count);
                foreach (XmlElement e in nl)
                {
                    if (e.HasAttribute("drawable"))
                    {
                        Bitmap b = getImage(e.GetAttribute("drawable"), size);
                        bitmapList.Add(b);
                    }
                    else
                    {
                        XmlElement shape = (XmlElement)e.SelectSingleNode("shape");
                        if (shape != null)
                        {
                            bitmapList.Add(parseShape(shape, size));
                        }
                        else
                        {
                            throw new Exception("unrecorgnized element in layer-list.");
                        }
                    }
                }
                Bitmap bmp = new Bitmap(size.Width, size.Height);
                Graphics g = Graphics.FromImage(bmp);
                foreach (Bitmap b in bitmapList)
                {
                    g.DrawImage(Utility.ResizeBitmap(b, new Size(bmp.Width, bmp.Height)), 0, 0);
                }
                return bmp;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message + "\nError happens during parsing layer-list: " + node.InnerXml);
            }
        }

        private Bitmap parseVectorDrawable(XmlNode node, Size size)
        {
            // Preferred path: translate the VectorDrawable to SVG and let the vendored
            // Svg.NET renderer do the drawing (transforms, gradients, clips are mature
            // there). The legacy GDI+ walker remains as fallback for anything the
            // translator refuses. Mirrors ApkQuickReader.parseVectorDrawable.
            try
            {
                XmlElement vec = node as XmlElement;
                if (vec == null) vec = node.SelectSingleNode("/vector") as XmlElement;
                if (vec != null)
                {
                    string svg = VectorDrawableToSvg.Translate(vec, new Func<string, XmlDocument>(ExtractCompressedXml));
                    Svg.SvgDocument svgDoc = Svg.SvgDocument.FromSvg<Svg.SvgDocument>(svg);
                    if (svgDoc != null)
                    {
                        using (Bitmap rendered = svgDoc.Draw(size.Width, size.Height))
                        {
                            if (rendered != null) return new Bitmap(rendered);
                        }
                    }
                }
            }
            catch (Exception exSvg)
            {
                Log("svg vector render fallback for " + size.Width + "px: " + exSvg.Message);
            }
            try
            {
                return parseVectorDrawableLegacy(node, size);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message + "\nError happens during parsing vectordrawable: " + node.InnerXml);
            }
        }

        private Bitmap parseVectorDrawableLegacy(XmlNode node, Size size)
        {
            try
            {
                XmlElement vector = (XmlElement)node.SelectSingleNode("/vector");
                float viewportWidth = 0, viewportHeight = 0;
                // hostile XML may carry "NaN"/garbage where a float is expected -
                // parse leniently and let ClampCanvas decide, never crash the icon thread
                viewportWidth = GetXmlFloat(vector, "viewportWidth", 0f);
                viewportHeight = GetXmlFloat(vector, "viewportHeight", 0f);
                if (viewportWidth == 0) viewportWidth = viewportHeight;
                if (viewportHeight == 0) viewportHeight = viewportWidth;
                // android:width/android:height and their units are advisory only for the
                // legacy renderer: the canvas is sized from the clamped viewport below and
                // resized to the caller's icon size afterwards. (Upstream parsed them into
                // locals it never read; the dead parse also rejected hostile width values
                // that the viewport path handles fine.)
                // viewport comes from attacker XML: a huge/NaN value would OOM explorer's
                // process via the intermediate canvas. Real adaptive vectors are ≤ 108dp.
                Bitmap b = new Bitmap(ClampCanvas(viewportWidth), ClampCanvas(viewportHeight));
                using (Graphics g = Graphics.FromImage(b))
                {
                    DrawVectorGroup(g, vector, new Matrix());
                }
                Bitmap legacyResized = Utility.ResizeBitmap(b, size);
                b.Dispose(); // native-size canvas is dead once resized
                return legacyResized;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message + "\nError happens during parsing vectordrawable: " + node.InnerXml);
            }
        }

        private static int ClampCanvas(float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v) || v <= 1f) return 1;
            // icons are rasterised at <=256px by every caller; 1024 keeps even
            // 8x supersampling honest while a hostile viewport can no longer
            // demand a 4096x4096 (64 MB) bitmap
            return (int)Math.Min(v, 1024f);
        }

        private static float GetXmlFloat(XmlElement elem, string name, float def)
        {
            if (!elem.HasAttribute(name)) return def;
            float v;
            return float.TryParse(elem.GetAttribute(name), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out v) ? v : def;
        }

        /// <summary>
        /// Fills paths of a &lt;vector&gt; or nested &lt;group&gt;, honouring the group
        /// transforms (scale/rotation about pivot, then translation) defined by Android
        /// VectorDrawable. Older code read only the paths of one first-level group and
        /// silently ignored every transform, misplacing artwork such as the adaptive
        /// foreground of 'Light Start' (drawn upper-left instead of centered).
        /// </summary>
        private void DrawVectorGroup(Graphics g, XmlElement parent, Matrix world)
        {
            foreach (XmlNode child in parent.ChildNodes)
            {
                XmlElement elem = child as XmlElement;
                if (elem == null) continue;
                if (elem.Name == "path")
                {
                    try
                    {
                        string pathdata = elem.GetAttribute("pathData");
                        if (pathdata == "") continue;
                        GraphicsPath gpath = VectorDrawableRender.Convert2Path(pathdata);
                        gpath.Transform(world);
                        Brush fill = null;
                        if (elem.HasAttribute("fillColor"))
                        {
                            string fillcolor = elem.GetAttribute("fillColor");
                            if (fillcolor.EndsWith(".xml"))
                            {//gradient or indirect color resource
                                try
                                {
                                    fill = parseGradient(fillcolor);
                                }
                                catch (Exception exGrad)
                                {
                                    Log("vector gradient failed, black fill: " + exGrad.Message.Split('\n')[0]);
                                }
                            }
                            else
                            {
                                try { fill = new SolidBrush(stringToColor(fillcolor)); }
                                catch { /* color resource ref - painted black below */ }
                            }
                        }
                        if (fill == null) fill = new SolidBrush(System.Drawing.Color.Black);
                        g.FillPath(fill, gpath);
                    }
                    catch (Exception exPath)
                    {
                        // one unparseable path (clipped/animated art) must not kill the icon
                        Log("vector path skipped: " + exPath.Message.Split('\n')[0]);
                    }
                }
                else if (elem.Name == "group")
                {
                    Matrix m = (Matrix)world.Clone();
                    float pivotX = GetXmlFloat(elem, "pivotX", 0f);
                    float pivotY = GetXmlFloat(elem, "pivotY", 0f);
                    float sx = GetXmlFloat(elem, "scaleX", 1f);
                    float sy = GetXmlFloat(elem, "scaleY", 1f);
                    float rot = GetXmlFloat(elem, "rotation", 0f);
                    // Android applies scale then rotation about the pivot, then translation;
                    // appending mirrors that order for point transformation.
                    if (sx != 1f || sy != 1f) m.Scale(sx, sy, MatrixOrder.Append);
                    if (pivotX != 0f || pivotY != 0f) m.Translate(-pivotX, -pivotY, MatrixOrder.Append);
                    if (rot != 0f) m.Rotate(rot, MatrixOrder.Append);
                    if (pivotX != 0f || pivotY != 0f) m.Translate(pivotX, pivotY, MatrixOrder.Append);
                    m.Translate(GetXmlFloat(elem, "translateX", 0f), GetXmlFloat(elem, "translateY", 0f), MatrixOrder.Append);
                    DrawVectorGroup(g, elem, m);
                    m.Dispose();
                }
            }
            world.Dispose();
        }

        private Bitmap parseAdaptiveIcon(XmlNode node, Size size)
        {
            /// Get adaptive - icon
            /// 
            /*
            <?xml version="1.0" encoding="utf-8"?>
            <adaptive-icon xmlns:android="http://schemas.android.com/apk/res/android">
            <background android:drawable="@drawable/ic_launcher_background" />
            <foreground android:drawable="@drawable/ic_launcher_foreground" />
            </adaptive-icon>
            */
            try
            {
                XmlElement elem = (XmlElement)node.SelectSingleNode("/adaptive-icon/background");
                Bitmap b = null, f = null;
                Bitmap bmp = new Bitmap(size.Width, size.Height);
                Graphics g = Graphics.FromImage(bmp);
                if (elem.HasAttribute("drawable"))
                {
                    b = getImage(elem.GetAttribute("drawable"), size);
                    if (b != null)
                        g.DrawImage(b, 0, 0);
                    else
                    {
                        Color c = stringToColor(elem.GetAttribute("drawable"));
                        g.FillRectangle(new SolidBrush(c), 0, 0, bmp.Width, bmp.Height);
                    }
                }
                elem = (XmlElement)node.SelectSingleNode("/adaptive-icon/foreground");
                if (elem.HasAttribute("drawable"))
                {
                    f = getImage(elem.GetAttribute("drawable"), size);
                    if (f != null)
                        g.DrawImage(f, 0, 0);
                    else
                    {
                        Color c = stringToColor(elem.GetAttribute("drawable"));
                        g.FillRectangle(new SolidBrush(c), 0, 0, bmp.Width, bmp.Height);
                    }
                }
                return bmp;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message + "\nError happening during parse AdaptiveIcon.");
            }
        }

        private Brush parseGradient(XmlNode node)
        {
            try
            {
                XmlElement gradient = (XmlElement)node.FirstChild;
                gradientType type = (gradientType)int.Parse(gradient.GetAttribute("type"));
                if (type == gradientType.linear)
                {
                    Color startC = stringToColor(gradient.GetAttribute("startColor"));
                    Color endC = stringToColor(gradient.GetAttribute("endColor"));
                    float startX = float.Parse(gradient.GetAttribute("startX"));
                    float startY = float.Parse(gradient.GetAttribute("startY"));
                    float endX = float.Parse(gradient.GetAttribute("endX"));
                    float endY = float.Parse(gradient.GetAttribute("endY"));
                    return new LinearGradientBrush(new PointF(startX, startY), new PointF(endX, endY), startC, endC);
                }
                else if (type == gradientType.radial)
                {
                    XmlNodeList points = node.SelectNodes("/gradient/item");
                    int cx = int.Parse(gradient.GetAttribute("centerX"));
                    int cy = int.Parse(gradient.GetAttribute("centerY"));
                    float radius = float.Parse(gradient.GetAttribute("gradientRadius"));
                    List<Color> colors = new List<Color>(points.Count);
                    List<float> offsets = new List<float>(points.Count);
                    foreach (XmlElement p in points)
                    {
                        string fillcolor = p.GetAttribute("color");
                        colors.Add(stringToColor(p.GetAttribute("color")));
                        offsets.Add(float.Parse(p.GetAttribute("offset")));
                    }
                    return new SolidBrush(colors[colors.Count - 1]);
                    //fill = new Media.RadialGradientBrush();
                }
                else if (type == gradientType.sweep)
                {
                    return null;
                }
                else
                {
                    throw new Exception("Unknown gradient type when parsing gradient xml");
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message + "\nError happened during parse Gradient " + node.InnerXml);
            }
        }

        private Brush parseGradient(string xml)
        {
            try
            {
                XmlDocument doc = ExtractCompressedXml(xml);
                if (doc.FirstChild.Name == "gradient")
                {
                    return parseGradient(doc);
                }
                else
                {
                    throw new Exception("Doesn't find a gradient XML");
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message + "\nError happens during parsing gradient xml " + xml);
            }
        }

        private Bitmap parseShape(XmlNode shapeNode, Size size)
        {
            try
            {
                Bitmap b;
                XmlElement eShape;
                if (shapeNode.NodeType == XmlNodeType.Document)
                    eShape = ((XmlDocument)shapeNode).DocumentElement;
                else
                    eShape = (XmlElement)shapeNode;
                shapeType type = (shapeType)int.Parse(eShape.GetAttribute("shape"));
                XmlElement eSize = (XmlElement)shapeNode.SelectSingleNode("size");
                if (eSize != null)
                    b = new Bitmap(int.Parse(eSize.GetAttribute("width")), int.Parse(eSize.GetAttribute("height")));
                else
                {
                    b = new Bitmap(size.Width, size.Height);
                }
                Graphics g = Graphics.FromImage(b);
                GraphicsPath p = new GraphicsPath();
                Brush brush;
                XmlElement ebrush = (XmlElement)eShape.SelectSingleNode("solid");
                if (ebrush != null)
                {
                    brush = new SolidBrush(stringToColor(ebrush.GetAttribute("color")));
                }
                else
                {
                    if ((ebrush = (XmlElement)eShape.SelectSingleNode("gradient")) != null)
                    {
                        brush = parseGradient(ebrush);
                    }
                    else
                        brush = new SolidBrush(Color.Black);
                }
                if (type == shapeType.rectangle)
                {
                    XmlElement corners = (XmlElement)eShape.SelectSingleNode("corners");
                    if (corners != null)
                    {
                        //ToDo, support round corner
                    }
                    p.AddRectangle(new RectangleF(0, 0, b.Width, b.Height));
                }
                else if (type == shapeType.oval)
                {
                    p.AddEllipse(0, 0, b.Width, b.Height);
                }
                else if (type == shapeType.line)
                {
                    // todo: support line

                }
                else if (type == shapeType.ring)
                {
                    // todo: support ring
                }
                else
                {
                    throw new Exception("Unsupported shape type: " + (int)type);
                }
                g.FillPath(brush, p);
                return Utility.ResizeBitmap(b, size);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message + "\nError happening during parsing shape:" + shapeNode.InnerXml);
            }
        }

        private Color stringToColor(string color)
        {
            color = color.Trim();
            if (color.StartsWith("#"))
            {
                return Color.FromArgb((int)uint.Parse(color.Substring(1), NumberStyles.HexNumber));
            }
            else
            {
                return Color.FromArgb((int)uint.Parse(color));
            }
        }


        public string getAttribute(string tag, string attr)
        {
            return QuickSearchManifestXml(tag, attr);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="attribute"></param>
        /// <returns></returns>
        public string QuickSearchManifestXml(string tag, string attribute)
        {
            return QuickSearchCompressedXml(manifest, tag, attribute);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xml">xml byte stream</param>
        /// <param name="xpath"></param>
        /// <param name="attribute"></param>
        /// <param name="sub"></param>
        /// <returns></returns>
        private string QuickSearchCompressedXml(byte[] xml, string xpath, string attribute)
        {
            using (MemoryStream ms = new MemoryStream(xml))
            using (BinaryReader br = new BinaryReader(ms))
            {
                Log("Search xml for " + xpath + " " + attribute);
                string[] pathl = xpath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                ms.Seek(8, SeekOrigin.Begin); // skip header, no doubt it's an xml chunk
                ApkResource result = new ApkResource(0);
                int tagDepth = 0;
                int matchDepth = 0;

                // XML_START_ELEMENT CHUNK
                while (ms.Position < ms.Length)
                {
                    long chunkPos = ms.Position;
                    RES_TYPE chunkType = (RES_TYPE)br.ReadInt16();
                    short headerSize = br.ReadInt16();
                    int chunkSize = br.ReadInt32();
                    if (chunkType == RES_TYPE.RES_XML_START_ELEMENT_TYPE)
                    {
                        tagDepth++;

                        ms.Seek(8 + 4, SeekOrigin.Current); // skip line number & comment / namespace
                        string tag_s = QuickSearchCompressedXmlStringPoolAndResMap(xml, br.ReadUInt32());
                        if (tagDepth <= pathl.Length && tag_s.ToUpper() == pathl[tagDepth - 1].ToUpper())
                        {
                            matchDepth++;

                            if (matchDepth == pathl.Length)
                            { // match, read attributes
                                int attributeStart = br.ReadInt16();
                                int attributeSize = br.ReadInt16();
                                int attributeCount = br.ReadInt16();
                                for (int i = 0; i < attributeCount; i++)
                                {
                                    int offset = headerSize + attributeStart + attributeSize * i + 4;
                                    if (offset >= chunkSize)
                                    { // Error: comes to out of chunk
                                        throw new Exception("Out of Chunk when processing tag " + tag_s);
                                    }
                                    ms.Seek(chunkPos + offset, SeekOrigin.Begin); // ignore the ns                            
                                    uint ind = br.ReadUInt32();
                                    string name = QuickSearchCompressedXmlStringPoolAndResMap(xml, ind);
                                    if (name.ToUpper() == attribute.ToUpper())
                                    {
                                        ms.Seek(4 + 2 + 1, SeekOrigin.Current); // skip rawValue/size/0/
                                        DATA_TYPE dataType = (DATA_TYPE)br.ReadByte();
                                        uint data = br.ReadUInt32();
                                        return convertData(xml, dataType, data);
                                    }
                                }
                            }
                        }
                    }
                    else if (chunkType == RES_TYPE.RES_XML_END_ELEMENT_TYPE)
                    {
                        if (matchDepth == tagDepth)
                        {
                            matchDepth--;
                        }
                        tagDepth--;
                    }
                    SeekNextChunk(ms, chunkPos, chunkSize);
                }
                return "";
            }
        }

        public XmlDocument ExtractCompressedXml(string path)
        {
            try
            {
                ZipEntry en = ZipSafety.GetEntryExact(zip, path);
                byte[] bytes = ZipSafety.ReadEntryBounded(zip, en, ZipSafety.MAX_XML, "xml " + path);
                return ExtractCompressedXml(bytes);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message + "\nError happens during dump " + path);
            }
        }

        private XmlDocument ExtractCompressedXml(byte[] bytes)
        {
            using (MemoryStream ms = new MemoryStream(bytes))
            using (BinaryReader br = new BinaryReader(ms))
            {
                long chunkPos = ms.Position;
                RES_TYPE chunkType = (RES_TYPE)br.ReadInt16();
                short headerSize = br.ReadInt16();
                int chunkSize = br.ReadInt32();
                ms.Seek(chunkPos + headerSize, SeekOrigin.Begin);

                XmlDocument doc = new XmlDocument();
                XmlNode currentNode = doc;
                XmlNamespaceManager nm = new XmlNamespaceManager(doc.NameTable);

                try
                {
                    // XML_START_ELEMENT CHUNK
                    while (ms.Position < ms.Length)
                    {
                        chunkPos = ms.Position;
                        chunkType = (RES_TYPE)br.ReadInt16();
                        headerSize = br.ReadInt16();
                        chunkSize = br.ReadInt32();
                        ms.Seek(chunkPos + headerSize, SeekOrigin.Begin);
                        if (chunkType == RES_TYPE.RES_XML_START_NAMESPACE_TYPE)
                        {
                            string prefix = QuickSearchStringPool(bytes, br.ReadUInt32());
                            string uri = QuickSearchStringPool(bytes, br.ReadUInt32());
                            nm.AddNamespace(prefix, uri);
                        }
                        else if (chunkType == RES_TYPE.RES_XML_CDATA_TYPE)
                        {
                            string cdatastr = QuickSearchStringPool(bytes, br.ReadUInt16());
                            XmlCDataSection cdata = doc.CreateCDataSection(cdatastr);
                            currentNode.AppendChild(cdata);
                        }
                        else if (chunkType == RES_TYPE.RES_XML_START_ELEMENT_TYPE)
                        {
                            string ns = QuickSearchCompressedXmlStringPoolAndResMap(bytes, br.ReadUInt32());
                            string tag_s = QuickSearchCompressedXmlStringPoolAndResMap(bytes, br.ReadUInt32());
                            XmlElement currentElem = doc.CreateElement(tag_s, ns);
                            currentNode.AppendChild(currentElem);
                            currentNode = currentElem;

                            int attributeStart = br.ReadInt16();
                            int attributeSize = br.ReadInt16();
                            int attributeCount = br.ReadInt16();
                            for (int i = 0; i < attributeCount; i++)
                            {
                                int offset = headerSize + attributeStart + attributeSize * i + 4;
                                ms.Seek(chunkPos + offset, SeekOrigin.Begin);
                                uint ind = br.ReadUInt32();
                                string name = QuickSearchCompressedXmlStringPoolAndResMap(bytes, ind);
                                ms.Seek(4 + 2 + 1, SeekOrigin.Current); // skip rawValue/size/0/
                                DATA_TYPE dataType = (DATA_TYPE)br.ReadByte();
                                uint data = br.ReadUInt32();
                                string val = convertData(bytes, dataType, data);
                                currentElem.SetAttribute(name, val);
                            }
                        }
                        else if (chunkType == RES_TYPE.RES_XML_END_ELEMENT_TYPE)
                        {
                            currentNode = currentNode.ParentNode;
                        }
                        SeekNextChunk(ms, chunkPos, chunkSize);
                    }
                    return doc;
                }
                catch (Exception ex)
                {
                    Log(doc.InnerXml);
                    throw new Exception(ex.Message + "\nError happens during dump xml file @ bytes " + ms.Position.ToString());
                }
            }
        }

        private string convertData(byte[] bytes, DATA_TYPE type, UInt32 data)
        {
            switch (type)
            {
                case DATA_TYPE.TYPE_STRING:
                    return QuickSearchStringPool(bytes, data);
                //case DATA_TYPE.TYPE_REFERENCE:
                //    try
                //    {
                //        ApkResource r = QuickSearchResource((UInt32)data);
                //        int ind = 0;
                //        if (r.Count > 1)
                //            ind = applyFilter(r);
                //        return r.values[ind].ToString();
                //    }
                //    catch (Exception ex)
                //    {
                //        Log("Error happen when finding resource with ID:0x" + data.ToString("X8") + ex.Message);
                //        return "(0x" + data.ToString("X8") + ")";
                //    }
                case DATA_TYPE.TYPE_INT_BOOLEAN:
                    return (data == 0) ? ConstTrue : ConstFalse;
                case DATA_TYPE.TYPE_DIMENSION:
                    COMPLEX_TYPE t = (COMPLEX_TYPE)(data & 0xff);
                    string unit = (t == COMPLEX_TYPE.COMPLEX_UNIT_DIP) ? "dp" :
                                  (t == COMPLEX_TYPE.COMPLEX_UNIT_IN) ? "in" :
                                  (t == COMPLEX_TYPE.COMPLEX_UNIT_MM) ? "mm" :
                                  (t == COMPLEX_TYPE.COMPLEX_UNIT_PX) ? "px" :
                                  (t == COMPLEX_TYPE.COMPLEX_UNIT_SP) ? "sp" :
                                  (t == COMPLEX_TYPE.COMPLEX_UNIT_PT) ? "pt" : "";
                    return (data >> 8).ToString() + unit;
                case DATA_TYPE.TYPE_FLOAT:
                    float f = BitConverter.ToSingle(BitConverter.GetBytes(data), 0);
                    return f.ToString();
                case DATA_TYPE.TYPE_INT_COLOR_ARGB8:
                case DATA_TYPE.TYPE_INT_COLOR_ARGB4:
                case DATA_TYPE.TYPE_INT_COLOR_RGB4:
                case DATA_TYPE.TYPE_INT_COLOR_RGB8:
                    string hex = data.ToString("X8");
                    return "#" + hex;
                default:
                    return data.ToString();
            }
        }

        // Guards the forward chunk walks below (same hazard as in ApkReader:
        // a chunk header claiming size < 8 spins the while loops forever and
        // hangs the shell UI thread).
        private static void SeekNextChunk(MemoryStream ms, long chunkPos, int chunkSize)
        {
            if (chunkSize < 8)
                throw new InvalidDataException("chunk at offset " + chunkPos + " claims size " + chunkSize);
            ms.Seek(chunkPos + chunkSize, SeekOrigin.Begin);
        }

        private string QuickSearchCompressedXmlStringPoolAndResMap(byte[] xml, uint id)
        {
            if (id == 0xffffffff) return "";
            string result = QuickSearchStringPool(xml, id);
            if (result == "")
            {
                result = QuickSearchCompressedXMlResMap(xml, id);
                if (result == "") result = "(0x" + id.ToString("X8") + ")";
            }
            return result;
        }

        private string QuickSearchStringPool(byte[] bytes, uint id)
        {
            if (id == 0xffffffff) return "";
            using (MemoryStream ms = new MemoryStream(bytes))
            using (BinaryReader br = new BinaryReader(ms))
            {
                RES_TYPE chunkType = (RES_TYPE)br.ReadInt16();
                short headerSize = br.ReadInt16();
                ms.Seek(headerSize, SeekOrigin.Begin);

                while (ms.Position < ms.Length)
                {
                    long chunkPos = ms.Position;
                    chunkType = (RES_TYPE)br.ReadInt16();
                    headerSize = br.ReadInt16();
                    int chunkSize = br.ReadInt32();
                    if (chunkType == RES_TYPE.RES_STRING_POOL_TYPE)
                    {
                        int stringcount = br.ReadInt32();
                        int stylecount = br.ReadInt32();
                        int flags = br.ReadInt32();
                        bool isUTF_8 = (flags & (1 << 8)) != 0;
                        int stringStart = br.ReadInt32();
                        ms.Seek(4 + id * 4, SeekOrigin.Current);
                        int stringPos = br.ReadInt32();
                        ms.Seek(chunkPos + stringStart + stringPos, SeekOrigin.Begin);
                        if (isUTF_8)
                        {
                            int u16len = br.ReadByte(); // u16len
                            if ((u16len & 0x80) != 0)
                            {// larger than 128
                                u16len = ((u16len & 0x7F) << 8) + br.ReadByte();
                            }

                            int u8len = br.ReadByte(); // u8len
                            if ((u8len & 0x80) != 0)
                            {// larger than 128
                                u8len = ((u8len & 0x7F) << 8) + br.ReadByte();
                            }
                            // a hostile pool entry may claim more bytes than the pool
                            // holds; ReadBytes would pre-allocate the claimed size (OOM)
                            if (u8len > ms.Length - ms.Position)
                                throw new InvalidDataException("string pool entry out of bounds");
                            return Encoding.UTF8.GetString(br.ReadBytes(u8len));
                        }
                        else // UTF_16
                        {
                            int u16len = br.ReadUInt16();
                            if ((u16len & 0x8000) != 0)
                            {// larger than 32768
                                u16len = ((u16len & 0x7FFF) << 16) + br.ReadUInt16();
                            }

                            if (u16len < 0 || u16len > (ms.Length - ms.Position) / 2)
                                throw new InvalidDataException("string pool entry out of bounds");
                            return Encoding.Unicode.GetString(br.ReadBytes(u16len * 2));
                        }
                    }
                    else
                    {
                        SeekNextChunk(ms, chunkPos, chunkSize);
                    }
                }
                return "";
            }
        }

        private string QuickSearchCompressedXMlResMap(byte[] xml, uint id)
        {
            if (id == 0xffffffff) return "";
            using (MemoryStream ms = new MemoryStream(xml))
            using (BinaryReader br = new BinaryReader(ms))
            {
                RES_TYPE chunkType = (RES_TYPE)br.ReadInt16();
                short headerSize = br.ReadInt16();
                ms.Seek(headerSize, SeekOrigin.Begin);

                while (ms.Position < ms.Length)
                {
                    long chunkPos = ms.Position;
                    chunkType = (RES_TYPE)br.ReadInt16();
                    headerSize = br.ReadInt16();
                    int chunkSize = br.ReadInt32();

                    if (chunkType == RES_TYPE.RES_XML_RESOURCE_MAP_TYPE)
                    {
                        //Resource map
                        ms.Seek(id * 4, SeekOrigin.Current);
                        string result = Enum.GetName(typeof(R_attr), br.ReadUInt32());
                        if (result != null)
                            return result;
                        else
                            return "";
                    }
                    else
                    {
                        SeekNextChunk(ms, chunkPos, chunkSize);
                    }
                }
                return "";
            }
        }

        /// <summary>
        /// Get culture info from string
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        private CultureInfo getCulture(byte[] code)
        {
            string language;
            string country;
            byte[] decode;
            if (code[1] > 0x80)
            { // ISO-639-2
                decode = new byte[3];
                decode[0] = (byte)(code[0] & 0x1F);
                decode[1] = (byte)(((code[1] & 0x3) << 3) + (code[0] & 0xE0) >> 1);
                decode[2] = (byte)((code[1] & 0x7C) >> 2);
            }
            else
            { //ISO-639-1
                decode = new byte[2];
                decode[0] = code[0];
                decode[1] = code[1];
            }
            language = System.Text.Encoding.ASCII.GetString(decode);
            decode = new byte[2];
            if (code[3] > 0x80)
            {
                decode[0] = (byte)(code[2] & 0x1F);
                decode[1] = (byte)(((code[3] & 0x3) << 3) + (code[2] & 0xE0) >> 1);
            }
            else
            {
                decode[0] = code[0];
                decode[1] = code[1];
            }
            country = System.Text.Encoding.ASCII.GetString(decode);
            return new CultureInfo(country + "-" + language);
        }

        //// for handling loop reference
        //private Stack<UInt32> searchstack = new Stack<UInt32>();
        ///// <summary>
        ///// Find the requested resource, according to config setting, if the config was set.
        ///// This method is NOT HANDLING ANY ERROR, yet!!!!
        ///// </summary>
        ///// <param name="id">resourceID</param>
        ///// <returns>the resource, in string format, if resource id not found, return null value</returns>
        //private ApkResource QuickSearchResource(UInt32 id)
        //{
        //    searchstack.Push(id);
        //    ApkResource res = new ApkResource(id);

        //    using (MemoryStream ms = new MemoryStream(resources))
        //    using (BinaryReader br = new BinaryReader(ms))
        //    {
        //        ms.Seek(8, SeekOrigin.Begin); // jump type/headersize/chunksize
        //        int packageCount = br.ReadInt32();
        //        // comes to stringpool chunk, skipit
        //        long stringPoolPos = ms.Position;
        //        ms.Seek(4, SeekOrigin.Current);
        //        int stringPoolSize = br.ReadInt32();
        //        ms.Seek(stringPoolSize - 8, SeekOrigin.Current); // jump to the end

        //        //Package chunk now
        //        for (int pack = 0; pack < packageCount; pack++)
        //        {
        //            long PackChunkPos = ms.Position;
        //            ms.Seek(2, SeekOrigin.Current); // jump type/headersize
        //            int headerSize = br.ReadInt16();
        //            int PackChunkSize = br.ReadInt32();
        //            int packID = br.ReadInt32();

        //            if (packID != res.PackageID)
        //            { // check if the resource is in this pack
        //              // goto next chunk
        //                ms.Seek(PackChunkPos + PackChunkSize, SeekOrigin.Begin);
        //                continue;
        //            }
        //            else
        //            {
        //                //ms.Seek(128*2, SeekOrigin.Current); // skip name
        //                //int typeStringsPos = br.ReadInt32();
        //                //ms.Seek(4,SeekOrigin.Current);    // skip lastpublictype
        //                //int keyStringsPos = br.ReadInt32();
        //                //ms.Seek(4, SeekOrigin.Current);  // skip lastpublickey
        //                ms.Seek(PackChunkPos + headerSize, SeekOrigin.Begin);

        //                // skip typestring chunk
        //                ms.Seek(4, SeekOrigin.Current);
        //                ms.Seek(br.ReadInt32() - 8, SeekOrigin.Current); // jump to the end
        //                                                                 // skip keystring chunk
        //                ms.Seek(4, SeekOrigin.Current);
        //                ms.Seek(br.ReadInt32() - 8, SeekOrigin.Current); // jump to the end

        //                // come to typespec chunks and type chunks
        //                // typespec and type chunks may happen in a row.
        //                do
        //                {
        //                    long chunkPos = ms.Position;
        //                    short chunkType = br.ReadInt16();
        //                    headerSize = br.ReadInt16();
        //                    int chunkSize = br.ReadInt32();
        //                    byte typeid;

        //                    if (chunkType == (short)RES_TYPE.RES_TABLE_TYPE_TYPE)
        //                    {
        //                        typeid = br.ReadByte();
        //                        if (typeid == res.TypeID)
        //                        {
        //                            ms.Seek(3, SeekOrigin.Current); // skip 0
        //                            int entryCount = br.ReadInt32();
        //                            int entryStart = br.ReadInt32();

        //                            // read the config section
        //                            int config_size = br.ReadInt32();
        //                            byte[] conf = br.ReadBytes(config_size - 4);

        //                            ms.Seek(chunkPos + headerSize + res.EntryID * 4, SeekOrigin.Begin);
        //                            //ms.Seek(EntryID * 4, SeekOrigin.Current); // goto index
        //                            uint entryIndic = br.ReadUInt32();
        //                            if (entryIndic == 0xffffffff)
        //                            {
        //                                ms.Seek(chunkPos + chunkSize, SeekOrigin.Begin);
        //                                continue; //no entry here, go to next chunk
        //                            }
        //                            ms.Seek(chunkPos + entryStart + entryIndic, SeekOrigin.Begin);

        //                            // get to the entry
        //                            ms.Seek(11, SeekOrigin.Current); // skip entry size, flags, key, size, 0
        //                            byte dataType = br.ReadByte();
        //                            uint data = br.ReadUInt32();
        //                            if (dataType == (byte)DATA_TYPE.TYPE_STRING)
        //                            {
        //                                res.Add(conf, QuickSearchStringPool(resources, data));
        //                            }
        //                            else if (dataType == (byte)DATA_TYPE.TYPE_REFERENCE)
        //                            {
        //                                // the entry is null, or it's referencing in loop, go to next chunk
        //                                if (data == 0x00000000 || searchstack.Contains(data))
        //                                {
        //                                    ms.Seek(chunkPos + chunkSize, SeekOrigin.Begin);
        //                                    continue;
        //                                }
        //                                res.Add(QuickSearchResource((UInt32)data));
        //                            }
        //                            else
        //                            { // I would like to expect we only will receive TYPE_STRING/TYPE_REFERENCE/any integer type, complex is not considering here,yet
        //                                res.Add(conf, data.ToString());
        //                            }
        //                        }
        //                    }
        //                    ms.Seek(chunkPos + chunkSize, SeekOrigin.Begin); // skip this chunk
        //                } while (ms.Position < PackChunkPos + PackChunkSize);
        //            }
        //        }
        //        searchstack.Pop();
        //        return res;
        //    }
        //}

        private int applyFilter(ApkResource r)
        {
            int best = 0;
            if (getFlag("Density") != null)
            {
                int bestDensity = 0;
                bool supAI = Utility.GetSetting("SupportAdaptiveIcon", "False") == "True";
                for (int i = 0; i < r.configs.Count; i++)
                {
                    if (!supAI)
                    {
                        if (r.values[i].ToString().EndsWith(".xml")) break;
                    }
                    byte[] bytes = r.configs[i];
                    int density = bytes[(int)ResourceConfig.Density + 1] * 256 + bytes[(int)ResourceConfig.Density];
                    if (density > bestDensity)
                    {
                        bestDensity = density;
                        best = i;
                    }
                }
            }
            return best;
        }

        #region IDispose
        private bool disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (disposed) return;
            if (disposing)
            {
                // resources = null;
                manifest = null;
                if (zip != null)
                    zip.Close();
            }
            disposed = true;
            base.Dispose(disposing);
        }

        public void Close()
        {
            Dispose(true);
        }

        ~AabReader()
        {
            Dispose(true);
        }
        #endregion
    }
}
