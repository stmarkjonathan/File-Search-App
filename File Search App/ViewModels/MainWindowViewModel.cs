using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using File_Search_App.Models;
using Microsoft.Win32;
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
        public ObservableCollection<MFTHandler.FileData> files;

        private Dictionary<string, MFTHandler.FileData> fileIndex;

        public MainWindowViewModel()
        {
            Files = new ObservableCollection<MFTHandler.FileData>();

            Files.Add(new MFTHandler.FileData() { FileIndex=0, FileName="File 1", ParentIndex=0});
            Files.Add(new MFTHandler.FileData() { FileIndex = 1, FileName = "File 2", ParentIndex = 0 });
            Files.Add(new MFTHandler.FileData() { FileIndex = 2, FileName = "File 3", ParentIndex = 1 });
        }

        [RelayCommand]
        private async Task ScanFiles()
        {
            Files = new ObservableCollection<MFTHandler.FileData>(await Task.Run(()=> MFTHandler.GetDriveFiles().Values));
            fileIndex = FileIndexHandler.IndexFiles(Files.ToList());
        }

        



        


    }
}
