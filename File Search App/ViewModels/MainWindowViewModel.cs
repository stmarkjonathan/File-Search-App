using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;


namespace File_Search_App.ViewModels
{
    internal partial class MainWindowViewModel : ObservableObject
    {

        private Dictionary<string, MFTHandler.FileData> fileIndex;

        public List<MFTHandler.FileData> filesList;

        [ObservableProperty]
        private ObservableCollection<MFTHandler.FileData> displayList;

        [ObservableProperty]
        private ObservableCollection<string> driveNames;

        [ObservableProperty]
        private string searchQuery;

        [ObservableProperty]
        private MFTHandler.FileData selectedFile;

        [ObservableProperty]
        private string selectedDrive;

        public MainWindowViewModel()
        {
            filesList = new List<MFTHandler.FileData>();

            filesList.Add(new MFTHandler.FileData() { FileIndex = 0, FileName = "File 1", FilePath = "File 1", ParentIndex = 0 });
            filesList.Add(new MFTHandler.FileData() { FileIndex = 1, FileName = "File 2", FilePath = "File 2", ParentIndex = 0 });
            filesList.Add(new MFTHandler.FileData() { FileIndex = 2, FileName = "File 3", FilePath = "File 3", ParentIndex = 1 });

            displayList = new ObservableCollection<MFTHandler.FileData>(filesList);

            fileIndex = FileIndexHandler.IndexFiles(filesList);

            driveNames = new ObservableCollection<string>(GetDriveNames());
            selectedDrive = driveNames[0];
        }

        [RelayCommand]
        private async Task ScanFiles()
        {
            filesList = new List<MFTHandler.FileData>(await Task.Run(() => MFTHandler.GetDriveFiles(SelectedDrive).Values));
            DisplayFiles(filesList);
            SetFileIcons(filesList);
            fileIndex = FileIndexHandler.IndexFiles(filesList);

        }

        [RelayCommand]
        private void OpenFile()
        {
            if (SelectedFile != null)
            {
                Process.Start("explorer.exe", SelectedFile.FilePath);
            }
        }

        [RelayCommand]
        private void OpenFileLocation()
        {
            if (SelectedFile != null)
            {
                Process.Start("explorer.exe", "/select," + SelectedFile.FilePath);
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

        private List<string> GetDriveNames()
        {
            DriveInfo[] drives = DriveInfo.GetDrives();
            List<string> driveList = new List<string>();

            foreach (var drive in drives)
            {
                driveList.Add(drive.Name);
            }

            return driveList;
        }

        private void SetFileIcons(List<MFTHandler.FileData> fileList)
        {
            foreach (var file in fileList)
            {
               //use win32 extract icons
            }
        }

        private ImageSource IconToImageSource(Icon icon)
        {
            ImageSource imageSource = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            return imageSource;
        }
    }
}
