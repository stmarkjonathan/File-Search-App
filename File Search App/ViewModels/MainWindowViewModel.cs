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

        private Dictionary<string, MFTHandler.FileData> fileIndex;

        public List<MFTHandler.FileData> Files;

        [ObservableProperty]
        private ObservableCollection<MFTHandler.FileData> displayList;

        [ObservableProperty]
        private string searchQuery;

        [ObservableProperty]
        private MFTHandler.FileData selectedFile;

        public MainWindowViewModel()
        {
            Files = new List<MFTHandler.FileData>();

            Files.Add(new MFTHandler.FileData() { FileIndex = 0, FileName = "File 1", FilePath="File 1", ParentIndex = 0 });
            Files.Add(new MFTHandler.FileData() { FileIndex = 1, FileName = "File 2", FilePath = "File 2", ParentIndex = 0 });
            Files.Add(new MFTHandler.FileData() { FileIndex = 2, FileName = "File 3", FilePath = "File 3", ParentIndex = 1 });

            DisplayList = new ObservableCollection<MFTHandler.FileData>(Files);

            fileIndex = FileIndexHandler.IndexFiles(Files);
        }

        [RelayCommand]
        private async Task ScanFiles()
        {
            Files = new List<MFTHandler.FileData>(await Task.Run(() => MFTHandler.GetDriveFiles().Values));
            DisplayFiles(Files);
            fileIndex = FileIndexHandler.IndexFiles(Files);
        }

        [RelayCommand]
        private void OpenFileLocation()
        {
            if (SelectedFile != null)
            {
                Process.Start("explorer.exe", "/select,"+ SelectedFile.FilePath);
            }            
        }

        public List<MFTHandler.FileData> SearchFiles()
        {

            List<MFTHandler.FileData> foundFiles = new List<MFTHandler.FileData>();
            
            if (!String.IsNullOrWhiteSpace(SearchQuery))
            {

                foreach (MFTHandler.FileData file in fileIndex.Values)
                {
                    if (file.FileName.IndexOf(SearchQuery, StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        foundFiles.Add(file);
                    }
                }
            }

            return foundFiles;
        }

        private void DisplayFiles(List<MFTHandler.FileData> files)
        {
            DisplayList = new ObservableCollection<MFTHandler.FileData>(files);
        }

        






    }
}
