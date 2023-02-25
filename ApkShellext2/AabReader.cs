using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using System.IO;

namespace ApkShellext2
{
    /// <summary>
    /// A Android App Bundler Reader
    /// Not implemented yet
    /// </summary>
    public class AabReader: AppPackageReader
    {
        private ZipFile zip;

        public AabReader(string path) 
        {
            FileName = path;
            openStream(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
        }


        private void openStream(Stream stream)
        {
            zip = new ZipFile(stream);
            
        }

        public override AppPackageReader.AppType Type
        {
            get
            {
                return AppType.AndroidAab;
            }
        }
    }
}
