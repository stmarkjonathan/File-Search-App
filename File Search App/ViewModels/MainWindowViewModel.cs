using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using File_Search_App.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    internal partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        public ObservableCollection<MFTHandler.FileInfo> files;

        public MainWindowViewModel()
        {
            Files = new ObservableCollection<MFTHandler.FileInfo>();

            Files.Add(new MFTHandler.FileInfo() { fileIndex=0, fileName="File 1", parentIndex=0});
            Files.Add(new MFTHandler.FileInfo() { fileIndex = 0, fileName = "File 2", parentIndex = 0 });
            Files.Add(new MFTHandler.FileInfo() { fileIndex = 0, fileName = "File 3", parentIndex = 0 });
        }

        [RelayCommand]
        private void ScanFiles() => Files = new ObservableCollection<MFTHandler.FileInfo>(MFTHandler.GetDriveFiles());




    }
}
