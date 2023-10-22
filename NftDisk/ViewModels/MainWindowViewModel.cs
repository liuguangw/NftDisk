using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Liuguang.Storage;
using ReactiveUI;

namespace Liuguang.NftDisk.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    #region Fields
    private bool _showModal = false;
    private long currentDirId = 0;
    private string currentDir = "/";
    private StorageDatabase? database = null;
    private readonly ObservableCollection<FileItem> fileItems = new();
    #endregion

    #region Properties
    public ObservableCollection<FileItem> FileItems => fileItems;
    public string CurrentDir
    {
        get => currentDir;
        set => this.RaiseAndSetIfChanged(ref currentDir, value);
    }

    public bool ShowModal
    {
        get => _showModal;
        set => this.RaiseAndSetIfChanged(ref _showModal, value);
    }

    public AskStringViewModel AskStringVm { get; } = new();
    public ReactiveCommand<FileItem, Unit> OpenDirOrShowFileLinksCommand { get; }
    public ReactiveCommand<Unit, Unit> GotoUpFolderCommand { get; }
    public bool CanGotoUpFolder => currentDirId != 0;
    #endregion

    public MainWindowViewModel()
    {
        OpenDirOrShowFileLinksCommand = ReactiveCommand.Create<FileItem>(OpenDirOrShowFileLinks);
        var canGotoUpFolder = this.WhenAnyValue(item => item.CanGotoUpFolder);
        GotoUpFolderCommand = ReactiveCommand.Create(GotoUpFolderAction, canGotoUpFolder);
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
    public async Task AskUploadFilesAsync(IEnumerable<IStorageItem> files)
    {
        List<string> itemNames = new();
        foreach (var item in files)
        {
            if (item is IStorageFile file)
            {
                itemNames.Add("文件: " + file.Name);
            }
            else if (item is IStorageFolder folder)
            {
                itemNames.Add("文件夹: " + folder.Name);
            }
        }
        //todo
        //DebugText = string.Join('\n', itemNames);
        await Task.Delay(100);
        throw new NotImplementedException();
    }

    /// <summary>
    /// 打开文件夹或者获取文件地址
    /// </summary>
    /// <param name="fileItem"></param>
    private async void OpenDirOrShowFileLinks(FileItem fileItem)
    {
        if (fileItem.ItemType == FileType.Dir)
        {
            await OpenFolderAsync(fileItem.ID);
            return;
        }
        //todo
        throw new NotImplementedException();
    }

    private async Task OpenFolderAsync(long pathID)
    {
        if (database is null)
        {
            return;
        }
        currentDirId = pathID;
        this.RaisePropertyChanged(nameof(CanGotoUpFolder));
        CurrentDir = await database.GetFullPathAsync(currentDirId);
        await LoadFileListAsync();
    }
    private async void GotoUpFolderAction()
    {
        if (database is null)
        {
            return;
        }
        var pathInfo = await database.GetFileInfoAsync(currentDirId);
        if (pathInfo is null)
        {
            return;
        }
        await OpenFolderAsync(pathInfo.ParentID);
    }

    public async void RefreshAction()
    {
        await OpenFolderAsync(currentDirId);
    }

    public async void GotoRootFolderAction()
    {
        await OpenFolderAsync(0);
    }

    public void CreateFolderAction()
    {
        AskStringVm.Title = "创建目录";
        AskStringVm.Label = "目录名";
        AskStringVm.Watermark = "请输入目录名";
        AskStringVm.InputText = string.Empty;
        AskStringVm.CompleteAction = () =>
        {
            AskStringVm.ShowModal = false;
            ShowModal = false;
            if (AskStringVm.Confirm)
            {
                ProcessCreateFolder(AskStringVm.InputText);
            }
        };
        ShowModal = true;
        AskStringVm.ShowModal = true;
    }

    private async void ProcessCreateFolder(string folderName)
    {
        if (database is null)
        {
            return;
        }
        var folderLog = new StorageFile(folderName)
        {
            ParentID = currentDirId,
            ItemType = FileType.Dir,
            Name = folderName,
        };
        folderLog.SyncTime();
        await database.InsertFileLog(folderLog);
        RefreshAction();
    }
}
