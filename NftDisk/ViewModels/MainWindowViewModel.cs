using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Liuguang.Storage;

namespace Liuguang.NftDisk.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public string Greeting => "Welcome to Avalonia!";
    private long currentDirId = 0;
    private StorageDatabase? database = null;
    private readonly ObservableCollection<FileItem> fileItems = new();
    public ObservableCollection<FileItem> FileItems => fileItems;

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
}
