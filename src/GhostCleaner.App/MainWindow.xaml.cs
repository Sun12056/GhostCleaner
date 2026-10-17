using System.Windows;
using GhostCleaner.App.ViewModels;

namespace GhostCleaner.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
