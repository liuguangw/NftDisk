using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Liuguang.NftDisk.ViewModels;

namespace Liuguang.NftDisk.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainView_Loaded;
        Closed += FreeResource;
    }

    private async void FreeResource(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.FreeResourceAsync();
        }
    }

    private async void MainView_Loaded(object? sender, RoutedEventArgs e)
    {
        FontFamily = FontManager.Current.DefaultFontFamily;
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.LoadFileListAsync();
        }
    }
}