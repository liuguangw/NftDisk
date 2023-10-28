using Avalonia.Controls;
using Avalonia.Input;
using Liuguang.NftDisk.Models;
using Liuguang.NftDisk.ViewModels;

namespace Liuguang.NftDisk.Views.Pages;

public partial class SaveDirPage : UserControl
{
    public SaveDirPage()
    {
        InitializeComponent();
    }
    private void OpenSubDir(object? sender, TappedEventArgs e)
    {
        if (sender is TextBlock textBlock)
        {
            if (textBlock.DataContext is SaveDirItem dirItem)
            {
                if (!dirItem.Enabled)
                {
                    return;
                }
                if (DataContext is SaveDirViewModel viewModel)
                {
                    viewModel.OpenSubDir(dirItem.ID);
                }
            }
        }
    }
}