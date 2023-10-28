using System;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Liuguang.NftDisk.Models;
using Liuguang.NftDisk.ViewModels;
using Liuguang.Storage;

namespace Liuguang.NftDisk.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainView_Loaded;
        Closed += FreeResource;
    }

    private void OpenFileItem(object? sender, TappedEventArgs e)
    {
        if (sender is TextBlock textBlock)
        {
            if (textBlock.DataContext is FileItem fileItem)
            {
                if (DataContext is MainWindowViewModel viewModel)
                {
                    viewModel.OpenDirOrShowFileLinksCommand.Execute(fileItem).Subscribe();
                }
            }
        }
    }

    private void CopyFileCid(object? sender, TappedEventArgs e)
    {
        if (sender is TextBlock textBlock)
        {
            if (textBlock.DataContext is FileItem fileItem)
            {
                if (fileItem.ItemType != FileType.File)
                {
                    return;
                }
                if (DataContext is MainWindowViewModel viewModel)
                {
                    viewModel.CopyCidCommand.Execute(fileItem).Subscribe();
                }
            }
        }
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
            await viewModel.OnLoadAsync();
        }
        this.Find<DataGrid>("MainGrid")?.AddHandler(DragDrop.DropEvent, ProcessDrop);
    }

    private void ProcessDrop(object? sender, DragEventArgs eventArgs)
    {

        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }
        if (eventArgs.Data.Contains(DataFormats.Files))
        {
            var files = eventArgs.Data.GetFiles();
            if (files is null)
            {
                return;
            }
            viewModel.AskUploadFiles(files);
        }
    }
}