using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace File_Search_App.Models
{
    internal static class SystemDrives
    {
        public static DriveInfo[] GetSystemDrives()
        {
            return DriveInfo.GetDrives();
        }
    }
}
