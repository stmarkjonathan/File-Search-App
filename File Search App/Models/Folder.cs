using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace File_Search_App.Models
{
    internal class Folder : SystemItem
    {

        private string name;
        private string path;

        public Folder(string name, string path)
        {
            this.name = name;
            this.path = path;
        }

        public override string GetName()
        {
            return name;
        }

        public override string GetPath()
        {
            return path;
        }
    }
}
