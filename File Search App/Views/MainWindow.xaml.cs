using File_Search_App.ViewModels;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace File_Search_App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {


        MainWindowViewModel ViewModel;
        public MainWindow()
        {

            DataContext = ViewModel = new MainWindowViewModel();
            InitializeComponent();
        }

        private async void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {         
            List<MFTHandler.FileData> list = new List<MFTHandler.FileData>(await Task.Run(() =>  ViewModel.SearchFiles() ));

            FilesDataGrid.ItemsSource = list;  

        }
    }
}