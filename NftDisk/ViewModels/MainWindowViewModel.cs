using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Liuguang.Storage;
using ReactiveUI;

namespace Liuguang.NftDisk.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private bool showDebug = false;
    public string debugText = "Welcome to Avalonia!";
    private long currentDirId = 0;
    private StorageDatabase? database = null;
    private readonly ObservableCollection<FileItem> fileItems = new();
    public ObservableCollection<FileItem> FileItems => fileItems;

    public string DebugText
    {
        get => debugText;
        set => this.RaiseAndSetIfChanged(ref debugText, value);
    }

    public bool ShowDebug
    {
        get => showDebug;
        set => this.RaiseAndSetIfChanged(ref showDebug, value);
    }

    public async Task LoadFileListAsync()
    {
        if (database is null)
        {
            database = new StorageDatabase("./data/storage.db");
            await database.OpenAsync();
        }
        var files = await database.GetFileListAsync(currentDirId);
        fileItems.Clear();
        foreach (var fileInfo in files)
        {
            if (fileInfo.ItemType == FileType.Dir)
            {
                fileItems.Add(new FileItem(
                    fileInfo.ID, fileInfo.ParentID,
                    fileInfo.Name, fileInfo.UploadTime
                ));
            }
            else if (fileInfo.ItemType == FileType.File)
            {
                fileItems.Add(new FileItem(
                    fileInfo.ID, fileInfo.ParentID,
                    fileInfo.Name, fileInfo.UploadTime,
                    fileInfo.CID, fileInfo.Size
                ));
            }
        }
    }

    public async Task FreeResourceAsync()
    {
        if (database is null)
        {
            return;
        }
        await database.CloseAsync();
    }

    /// <summary>
    /// 询问是否上传这些文件、目录
    /// </summary>
    /// <param name="files"></param>
    /// <exception cref="NotImplementedException"></exception>
    public async Task AskUploadFilesAsync(IEnumerable<IStorageItem> files)
    {
        List<string> itemNames = new();
        foreach (var item in files)
        {
            if (item is IStorageFile file)
            {
                itemNames.Add("file: " + file.Path.LocalPath);
            }
            else if (item is IStorageFolder folder)
            {
                itemNames.Add("dir: " + folder.Path.LocalPath);
            }
        }
        DebugText = string.Join('\n', itemNames);
        ShowDebug = true;
        await Task.Delay(3500);
        ShowDebug = false;
    }
}
