using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Liuguang.NftDisk.Config;
using Liuguang.Storage;
using ReactiveUI;

namespace Liuguang.NftDisk.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    #region Fields
    private bool _showModal = false;
    private long _currentDirId = 0;
    private string _currentDir = "/";
    private bool _isSelectAll = false;
    private StorageDatabase? _database = null;
    private readonly ObservableCollection<FileItem> _fileItems = new();
    #endregion

    #region Properties
    public ObservableCollection<FileItem> FileItems => _fileItems;
    public string CurrentDir
    {
        get => _currentDir;
        set => this.RaiseAndSetIfChanged(ref _currentDir, value);
    }

    public bool ShowModal
    {
        get => _showModal;
        set => this.RaiseAndSetIfChanged(ref _showModal, value);
    }

    public bool IsSelectAll
    {
        get => _isSelectAll;
        set
        {
            if (_isSelectAll != value)
            {
                _isSelectAll = value;
                this.RaisePropertyChanged();
                ProcessSelectAll(value);
            }
        }
    }

    public bool HasSelection
    {
        get
        {
            foreach (var item in _fileItems)
            {
                if (item.Selected)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public AskStringViewModel AskStringVm { get; } = new();

    public AskUploadViewModel AskUploadVm { get; } = new();
    public UploadListViewModel UploadListVm { get; } = new();
    public SettingViewModel SettingVm { get; } = new();
    public DownloadUrlViewModel DownloadUrlVm { get; } = new();
    public ConfirmViewModel ConfirmVm { get; } = new();
    public MsgTipViewModel MsgTipVm => MsgTipViewModel.Instance;
    public ReactiveCommand<FileItem, Unit> OpenDirOrShowFileLinksCommand { get; }
    public ReactiveCommand<FileItem, Unit> CopyCidCommand { get; }
    public ReactiveCommand<FileItem, Unit> RenameCommand { get; }
    public ReactiveCommand<FileItem, Unit> DeleteItemCommand { get; }
    /// <summary>
    /// 返回上一级文件夹
    /// </summary>
    public ReactiveCommand<Unit, Unit> GotoUpFolderCommand { get; }
    public bool CanGotoUpFolder => _currentDirId != 0;
    public ReactiveCommand<Unit, Unit> MutiDeleteItemCommand { get; }
    #endregion

    public MainWindowViewModel()
    {
        OpenDirOrShowFileLinksCommand = ReactiveCommand.Create<FileItem>(OpenDirOrShowFileLinks);
        CopyCidCommand = ReactiveCommand.Create<FileItem>(CopyCidAction);
        RenameCommand = ReactiveCommand.Create<FileItem>(RenameAction);
        DeleteItemCommand = ReactiveCommand.Create<FileItem>(DeleteAction);
        var canGotoUpFolder = this.WhenAnyValue(item => item.CanGotoUpFolder);
        GotoUpFolderCommand = ReactiveCommand.Create(GotoUpFolderAction, canGotoUpFolder);
        var canMutiDelete = this.WhenAnyValue(item => item.HasSelection);
        MutiDeleteItemCommand = ReactiveCommand.Create(MutiDeleteAction, canMutiDelete);


        UploadListVm.UploadSuccessAction = ProcessFileUploadSuccess;
        _fileItems.CollectionChanged += FileItemListChanged;
    }

    public async Task OnLoadAsync()
    {
        if (_database is null)
        {
            _database = new StorageDatabase("./data/storage.db");
            await _database.OpenAsync();
        }
        await LoadFileListAsync();
        await LoadConfigAsync();
        _ = Task.Run(() => UploadListVm.StartUploadListAsync());
    }

    private static async Task LoadConfigAsync()
    {
        await ApiTokenConfig.LoadAsync(ApiTokenConfig.DEFAULT_PATH);
        await GatewayConfig.LoadAsync(GatewayConfig.DEFAULT_PATH);
    }

    private async Task LoadFileListAsync()
    {
        if (_database is null)
        {
            return;
        }
        var files = await _database.GetFileListAsync(_currentDirId);
        _fileItems.Clear();
        foreach (var fileInfo in files)
        {
            FileItem? item = null;
            if (fileInfo.ItemType == FileType.Dir)
            {
                item = new FileItem(
                    fileInfo.ID, fileInfo.ParentID,
                    fileInfo.Name, fileInfo.UploadTime
                );
            }
            else if (fileInfo.ItemType == FileType.File)
            {
                item = new FileItem(
                    fileInfo.ID, fileInfo.ParentID,
                    fileInfo.Name, fileInfo.UploadTime,
                    fileInfo.CID, fileInfo.Size
                );
            }
            if (item is not null)
            {
                _fileItems.Add(item);
                item.PropertyChanged += FileItemPropertyChanged;
            }
        }
    }

    private void FileItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is FileItem fileItem)
        {
            if (e.PropertyName == nameof(fileItem.Selected))
            {
                if (!fileItem.Selected && _isSelectAll)
                {
                    //取消全选的勾选状态
                    SetSelectAllStatus(false);
                }
                this.RaisePropertyChanged(nameof(HasSelection));
            }
        }
    }

    private void FileItemListChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        //取消全选的勾选状态
        if (_isSelectAll)
        {
            SetSelectAllStatus(false);
        }
        this.RaisePropertyChanged(nameof(HasSelection));
    }

    public async Task FreeResourceAsync()
    {
        UploadListVm.Stop();
        if (_database is null)
        {
            return;
        }
        await _database.CloseAsync();
    }

    /// <summary>
    /// 询问是否上传这些文件、目录
    /// </summary>
    /// <param name="files"></param>
    public void AskUploadFiles(IEnumerable<IStorageItem> files)
    {
        var itemList = AskUploadVm.LocalItemList;
        itemList.Clear();
        List<IStorageFile> fileList = new();
        List<IStorageFolder> dirList = new();
        foreach (var item in files)
        {
            if (item is IStorageFile file)
            {
                itemList.Add(new(file.Name, true));
                fileList.Add(file);
            }
            else if (item is IStorageFolder folder)
            {
                itemList.Add(new(folder.Name, false));
                dirList.Add(folder);
            }
        }
        AskUploadVm.CompleteAction = () =>
        {
            ShowModal = false;
            if (AskUploadVm.Confirm)
            {
                if (!UploadListVm.ShowModal)
                {
                    UploadListVm.ShowDialog();
                }
                Task.Run(() => AddUploadTaskAsync(_currentDirId, fileList, dirList));
            }
        };
        if (itemList.Count > 0)
        {
            ShowModal = true;
            AskUploadVm.ShowModal = true;
        }
    }

    private async Task AddUploadTaskAsync(long parentDirID, List<IStorageFile> fileList, List<IStorageFolder> dirList)
    {
        AddUploadFilelistTask(parentDirID, fileList);
        await AddUploadDirlistTaskAsync(parentDirID, dirList);
    }

    private void AddUploadFilelistTask(long parentDirID, List<IStorageFile> fileList)
    {
        foreach (var file in fileList)
        {
            var uploadTask = new UploadFileItem(parentDirID, file);
            Dispatcher.UIThread.Invoke(() =>
            {
                UploadListVm.TaskList.Add(uploadTask);
            });
        }
    }

    private async Task AddUploadDirlistTaskAsync(long parentDirID, List<IStorageFolder> dirList)
    {
        foreach (var dirInfo in dirList)
        {
            await AddUploadDirTaskAsync(parentDirID, dirInfo);
        }
    }

    private async Task AddUploadDirTaskAsync(long parentDirID, IStorageFolder dirInfo)
    {
        if (_database is null)
        {
            return;
        }
        //判断文件夹是否存在
        var dirName = dirInfo.Name;
        var dbDirInfo = await _database.GetFileInfoAsync(parentDirID, dirName);
        if (dbDirInfo is null)
        {
            //创建文件夹
            dbDirInfo = new StorageFile(dirName)
            {
                ParentID = parentDirID,
                ItemType = FileType.Dir,
                Name = dirName,
            };
            dbDirInfo.SyncTime();
            await _database.InsertFileLog(dbDirInfo);
            //刷新列表
            if (parentDirID == _currentDirId)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    RefreshAction();
                });
            }
        }
        var itemParentDirID = dbDirInfo.ID;
        List<IStorageFile> subFileList = new();
        List<IStorageFolder> subDirList = new();
        var itemList = dirInfo.GetItemsAsync();
        await foreach (var item in itemList)
        {
            if (item is IStorageFile subFile)
            {
                subFileList.Add(subFile);
            }
            else if (item is IStorageFolder subDir)
            {
                subDirList.Add(subDir);
            }
        }

        AddUploadFilelistTask(itemParentDirID, subFileList);
        await AddUploadDirlistTaskAsync(itemParentDirID, subDirList);
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
        }
        else if (fileItem.ItemType == FileType.File)
        {
            ShowFileLinks(fileItem);
        }
    }

    private async Task OpenFolderAsync(long pathID)
    {
        if (_database is null)
        {
            return;
        }
        _currentDirId = pathID;
        this.RaisePropertyChanged(nameof(CanGotoUpFolder));
        CurrentDir = await _database.GetFullPathAsync(_currentDirId);
        await LoadFileListAsync();
    }

    /// <summary>
    /// 显示文件的下载地址
    /// </summary>
    /// <param name="fileItem"></param>
    private void ShowFileLinks(FileItem fileItem)
    {
        DownloadUrlVm.CompleteAction = () =>
        {
            ShowModal = false;
        };
        ShowModal = true;
        DownloadUrlVm.ShowDialog(fileItem.CID, fileItem.Name);
    }

    /// <summary>
    /// 复制文件cid
    /// </summary>
    /// <param name="fileItem"></param>
    private async void CopyCidAction(FileItem fileItem)
    {
        if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var clipboard = desktop.MainWindow!.Clipboard;
            if (clipboard is null)
            {
                return;
            }
            try
            {

                await clipboard.SetTextAsync(fileItem.CID);
                MsgTipVm.ShowDialog(true, $"复制文件{fileItem.Name}的CID成功");
            }
            catch (Exception ex)
            {
                MsgTipVm.ShowDialog(true, $"复制文件{fileItem.Name}的CID失败, {ex.Message}");
            }
        }
    }

    private async void GotoUpFolderAction()
    {
        if (_database is null)
        {
            return;
        }
        var pathInfo = await _database.GetFileInfoAsync(_currentDirId);
        if (pathInfo is null)
        {
            return;
        }
        await OpenFolderAsync(pathInfo.ParentID);
    }

    public async void RefreshAction()
    {
        await OpenFolderAsync(_currentDirId);
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
        if (_database is null)
        {
            return;
        }
        //检测目录是否已经存在
        var tLog = await _database.GetFileInfoAsync(_currentDirId, folderName);
        if (tLog is not null)
        {
            MsgTipVm.ShowDialog(false, $"目录{folderName}已存在");
            return;
        }
        //
        var folderLog = new StorageFile(folderName)
        {
            ParentID = _currentDirId,
            ItemType = FileType.Dir,
            Name = folderName,
        };
        folderLog.SyncTime();
        try
        {
            await _database.InsertFileLog(folderLog);
        }
        catch (Exception ex)
        {
            MsgTipVm.ShowDialog(false, $"创建目录{folderName}失败, {ex.Message}");
            return;
        }
        MsgTipVm.ShowDialog(true, $"创建目录{folderName}成功");
        RefreshAction();
    }

    /// <summary>
    /// 重命名
    /// </summary>
    /// <param name="fileItem"></param>
    private void RenameAction(FileItem fileItem)
    {
        var typeText = fileItem.ItemType == FileType.Dir ? "目录" : "文件";
        AskStringVm.Label = typeText + "名";
        AskStringVm.Title = "重命名" + typeText;
        AskStringVm.Watermark = "请输入新的" + typeText + "名";
        AskStringVm.InputText = fileItem.Name;
        AskStringVm.CompleteAction = () =>
        {
            ShowModal = false;
            if (AskStringVm.Confirm)
            {
                ProcessRenameItem(fileItem, AskStringVm.InputText);
            }
        };
        ShowModal = true;
        AskStringVm.ShowModal = true;
    }

    private async void ProcessRenameItem(FileItem fileItem, string newName)
    {
        if (fileItem.Name == newName)
        {
            return;
        }
        if (_database is null)
        {
            return;
        }
        //检测是否已经存在
        var tLog = await _database.GetFileInfoAsync(fileItem.ParentID, newName);
        if (tLog is not null)
        {
            MsgTipVm.ShowDialog(false, $"{newName}已存在");
            return;
        }
        //
        try
        {
            await _database.UpdateFilenameAsync(fileItem.ID, newName);
        }
        catch (Exception ex)
        {
            MsgTipVm.ShowDialog(false, $"重命名失败, {ex.Message}");
            return;
        }
        fileItem.Name = newName;
        MsgTipVm.ShowDialog(true, "重命名成功");
        RefreshAction();
    }

    /// <summary>
    /// 删除文件或者文件夹
    /// </summary>
    /// <param name="fileItem"></param>
    private void DeleteAction(FileItem fileItem)
    {
        ConfirmVm.CompleteAction = () =>
        {
            ShowModal = false;
            if (ConfirmVm.Confirm)
            {
                ProcessDeleteItem(fileItem);
            }
        };
        ShowModal = true;
        var content = string.Format("确定要删除{0}{1}吗?", fileItem.ItemType == FileType.Dir ? "文件夹" : "文件", fileItem.Name);
        ConfirmVm.ShowDialog("删除确认", content);
    }

    private async void ProcessDeleteItem(FileItem fileItem)
    {
        if (_database is null)
        {
            return;
        }
        if (fileItem.ItemType == FileType.File)
        {
            try
            {
                await _database.DeleteItemAsync(fileItem.ID);
            }
            catch (Exception ex)
            {
                MsgTipVm.ShowDialog(false, $"删除文件{fileItem.Name}失败, {ex.Message}");
                return;
            }
            _fileItems.Remove(fileItem);
            MsgTipVm.ShowDialog(true, $"删除文件{fileItem.Name}成功");
        }
        else if (fileItem.ItemType == FileType.Dir)
        {
            try
            {
                await _database.DeleteFolderAsync(fileItem.ID);
            }
            catch (Exception ex)
            {
                MsgTipVm.ShowDialog(false, $"删除文件夹{fileItem.Name}失败, {ex.Message}");
                return;
            }
            _fileItems.Remove(fileItem);
            MsgTipVm.ShowDialog(true, $"删除文件夹{fileItem.Name}成功");
        }
    }

    /// <summary>
    /// 批量删除
    /// </summary>
    private void MutiDeleteAction()
    {
        var selectedItems = (from tmpItem in _fileItems where tmpItem.Selected select tmpItem).ToList();
        if (selectedItems.Count == 1)
        {
            DeleteAction(selectedItems[0]);
            return;
        }
        ConfirmVm.CompleteAction = () =>
        {
            ShowModal = false;
            if (ConfirmVm.Confirm)
            {
                ProcessMutiDeleteItem(selectedItems);
            }
        };
        ShowModal = true;
        ConfirmVm.ShowDialog("删除确认", "确定要删除选择的文件/文件夹吗?");

    }

    private async void ProcessMutiDeleteItem(List<FileItem> selectedItems)
    {
        if (_database is null)
        {
            return;
        }
        foreach (var fileItem in selectedItems)
        {
            if (fileItem.ItemType == FileType.Dir)
            {
                try
                {
                    await _database.DeleteFolderAsync(fileItem.ID);
                }
                catch (Exception ex)
                {
                    MsgTipVm.ShowDialog(false, $"删除文件夹{fileItem.Name}失败, {ex.Message}");
                    return;
                }
                _fileItems.Remove(fileItem);
            }
            else if (fileItem.ItemType == FileType.File)
            {
                try
                {
                    await _database.DeleteItemAsync(fileItem.ID);
                }
                catch (Exception ex)
                {
                    MsgTipVm.ShowDialog(false, $"删除文件{fileItem.Name}失败, {ex.Message}");
                    return;
                }
                _fileItems.Remove(fileItem);
            }
        }
        MsgTipVm.ShowDialog(true, $"批量删除成功");
    }

    /// <summary>
    /// 切换任务列表的显示、隐藏状态
    /// </summary>
    public async void SwitchTaskListAction()
    {
        if (UploadListVm.ShowModal)
        {
            await UploadListVm.HideDialogAsync();
        }
        else
        {
            UploadListVm.ShowDialog();
        }
    }

    /// <summary>
    /// 打开设置界面
    /// </summary>
    public void ShowSettingAction()
    {
        SettingVm.CompleteAction = () =>
        {
            ShowModal = false;
        };
        ShowModal = true;
        SettingVm.ShowDialog();
    }

    private async void ProcessFileUploadSuccess(UploadFileItem item)
    {
        if (_database is null)
        {
            return;
        }
        var itemLog = new StorageFile(item.FileName)
        {
            ParentID = item.FolderID,
            CID = item.CID,
            Size = item.FileSize,
        };
        itemLog.SyncTime();
        await _database.InsertFileLog(itemLog);
        if (item.FolderID == _currentDirId)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                RefreshAction();
            });
        }
    }

    /// <summary>
    /// 设置全选框的状态
    /// </summary>
    /// <param name="isSelectAll"></param>
    private void SetSelectAllStatus(bool isSelectAll)
    {
        _isSelectAll = isSelectAll;
        this.RaisePropertyChanged(nameof(IsSelectAll));
    }

    /// <summary>
    /// 处理用户点击全选
    /// </summary>
    private void ProcessSelectAll(bool isSelectAll)
    {
        foreach (var item in _fileItems)
        {
            item.Selected = isSelectAll;
        }
    }
}
