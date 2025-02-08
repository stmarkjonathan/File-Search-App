using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace File_Search_App
{
    internal class FileIndexHandler
    {

        public static Dictionary<string, MFTHandler.FileData> IndexFiles(List<MFTHandler.FileData> files)
        {
            Dictionary<string, MFTHandler.FileData> fileIndex = new Dictionary<string, MFTHandler.FileData> ();

            foreach(MFTHandler.FileData file in files)
            {
                if(file != null)
                {
                    fileIndex[file.FilePath] = file;
                }              
            }

            return fileIndex;
        }


    }
}
