using System.Windows;
using WinCleaner.App.ViewModels;

namespace WinCleaner.App;

public partial class MainWindow : Window
{
    public MainWindow(ViewModels.MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
