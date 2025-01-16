using CommunityToolkit.Mvvm.ComponentModel;
using File_Search_App.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;


namespace File_Search_App.ViewModels
{
    internal class MainWindowViewModel : ObservableObject
    {

        public List<string> Names { get; set; }

        public MainWindowViewModel()
        {
            Names = new List<string>();
            MFTHandler mft = new MFTHandler();
        }


        

    }
}
