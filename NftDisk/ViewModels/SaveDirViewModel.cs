using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using Liuguang.NftDisk.Models;
using Liuguang.Storage;
using ReactiveUI;

namespace Liuguang.NftDisk.ViewModels;

public class SaveDirViewModel : ViewModelBase
{
    #region Fields
    private bool _showModal = false;
    private bool _confirm = false;
    private Action? _completeAction = null;
    private string _title = "标题";
    private long _currentDirId = 0;
    private string _currentDir = "/";
    private long _sourceDirId = 0;
    private readonly List<long> _disabledDirs = new();
    private readonly ObservableCollection<SaveDirItem> _saveDirList = new();
    private Func<StorageDatabase?> _databaseFunc;
    #endregion

    #region Properties
    public ObservableCollection<SaveDirItem> SaveDirList => _saveDirList;
    public long CurrentDirID => _currentDirId;
    public string Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }
    public string CurrentDir
    {
        get => _currentDir;
        private set => this.RaiseAndSetIfChanged(ref _currentDir, value);
    }
    public bool CurrentDirEnabled => _currentDirId != _sourceDirId;
    public bool ShowModal
    {
        get => _showModal;
        private set => this.RaiseAndSetIfChanged(ref _showModal, value);
    }

    public bool Confirm => _confirm;

    public Action CompleteAction { set => _completeAction = value; }
    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    /// <summary>
    /// 返回上一级文件夹
    /// </summary>
    public ReactiveCommand<Unit, Unit> GotoUpFolderCommand { get; }
    public bool CanGotoUpFolder => _currentDirId != 0;
    #endregion

    public SaveDirViewModel(Func<StorageDatabase?> databaseFunc)
    {
        _databaseFunc = databaseFunc;
        var canConfirm = this.WhenAnyValue(item => item.CurrentDirEnabled);
        ConfirmCommand = ReactiveCommand.Create(ProcessConfirmAction, canConfirm);
        CancelCommand = ReactiveCommand.Create(ProcessCancelAction);
        var canGotoUpFolder = this.WhenAnyValue(item => item.CanGotoUpFolder);
        GotoUpFolderCommand = ReactiveCommand.Create(GotoUpFolderAction, canGotoUpFolder);
    }

    public async void ShowDialog(string title, List<FileItem> selectedItems)
    {
        Title = title;
        ShowModal = true;
        _sourceDirId = 0;
        _disabledDirs.Clear();
        foreach (var item in selectedItems)
        {
            if (_sourceDirId != item.ParentID)
            {
                _sourceDirId = item.ParentID;
            }
            if (item.ItemType == FileType.Dir)
            {
                _disabledDirs.Add(item.ID);
            }
        }
        _currentDirId = _sourceDirId;
        this.RaisePropertyChanged(nameof(CurrentDirEnabled));
        //加载文件夹列表
        await LoadDirListAsync();
    }

    public void HideDialog()
    {
        ShowModal = false;
    }

    private async Task LoadDirListAsync()
    {
        var database = _databaseFunc.Invoke();
        if (database is null)
        {
            return;
        }
        _saveDirList.Clear();
        var dirList = await database.GetDirListAsync(_currentDirId);
        foreach (var dirInfo in dirList)
        {
            bool enabled = true;
            foreach (var disabledId in _disabledDirs)
            {
                if (dirInfo.ID == disabledId)
                {
                    enabled = false;
                    break;
                }
            }
            var saveDirItem = new SaveDirItem(OpenSubDir)
            {
                ID = dirInfo.ID,
                ParentID = dirInfo.ParentID,
                Name = dirInfo.Name,
                Enabled = enabled
            };
            _saveDirList.Add(saveDirItem);
        }
    }

    private async Task OpenFolderAsync(long pathID)
    {
        var database = _databaseFunc.Invoke();
        if (database is null)
        {
            return;
        }
        _currentDirId = pathID;
        this.RaisePropertyChanged(nameof(CanGotoUpFolder));
        this.RaisePropertyChanged(nameof(CurrentDirEnabled));
        CurrentDir = await database.GetFullPathAsync(_currentDirId);
        await LoadDirListAsync();
    }

    public async void OpenSubDir(long pathID)
    {
        await OpenFolderAsync(pathID);
    }
    private async void GotoUpFolderAction()
    {
        var database = _databaseFunc.Invoke();
        if (database is null)
        {
            return;
        }
        var pathInfo = await database.GetFileInfoAsync(_currentDirId);
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
    private void ProcessConfirmAction()
    {
        _confirm = true;
        _completeAction?.Invoke();
    }

    private void ProcessCancelAction()
    {
        _confirm = false;
        _completeAction?.Invoke();
    }
}