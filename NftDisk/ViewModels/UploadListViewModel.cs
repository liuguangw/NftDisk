using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using Liuguang.NftDisk.Config;
using Liuguang.NftDisk.Models;
using ReactiveUI;

namespace Liuguang.NftDisk.ViewModels;
public class UploadListViewModel : ViewModelBase
{
    const uint MAX_ACTIVE_TASK_COUNT = 2;
    private readonly ObservableCollection<UploadFileItem> _taskList = new();
    public ObservableCollection<UploadFileItem> TaskList => _taskList;
    private List<UploadFileItem> _activeTaskList = new();
    private bool _stopped = true;
    private bool _showModal = false;
    private Action<UploadFileItem>? _uploadSuccessAction = null;

    private bool _isStyleHidden = true;

    public bool IsStyleHidden
    {
        get => _isStyleHidden;
        set => this.RaiseAndSetIfChanged(ref _isStyleHidden, value);
    }

    public bool ShowModal
    {
        get => _showModal;
        private set => this.RaiseAndSetIfChanged(ref _showModal, value);
    }

    public Action<UploadFileItem> UploadSuccessAction { set => _uploadSuccessAction = value; }

    public void ShowDialog()
    {
        ShowModal = true;
        IsStyleHidden = false;
    }

    public async Task HideDialogAsync()
    {
        IsStyleHidden = true;
        await Task.Delay(200);
        ShowModal = false;
    }

    public async void CloseAction()
    {
        await HideDialogAsync();
    }

    public void Stop()
    {
        _stopped = true;
    }

    public async Task StartUploadListAsync()
    {
        _stopped = false;
        while (!_stopped)
        {
            uint activeTaskCount = 0;
            var toRemoveList = new List<UploadFileItem>();
            foreach (var taskInfo in _activeTaskList)
            {
                //从激活的任务列表中删除已经完成的任务或者停止的任务
                if (taskInfo.Status == UploadStatus.Success || taskInfo.Status == UploadStatus.Failed || taskInfo.Status == UploadStatus.Stopped)
                {
                    toRemoveList.Add(taskInfo);
                }
                else
                {
                    activeTaskCount++;
                }
            }
            if (toRemoveList.Count > 0)
            {
                _activeTaskList.RemoveMany(toRemoveList);
            }
            //需要添加任务
            if (activeTaskCount < MAX_ACTIVE_TASK_COUNT)
            {
                AddActiveTask(activeTaskCount, MAX_ACTIVE_TASK_COUNT);
            }
            await Task.Delay(200);
        }
    }

    private void AddActiveTask(uint activeTaskCount, uint maxCount)
    {
        uint tCount = activeTaskCount;
        foreach (var taskInfo in _taskList)
        {
            if (tCount >= maxCount)
            {
                break;
            }
            if (taskInfo.Status == UploadStatus.Pending)
            {
                _activeTaskList.Add(taskInfo);
                Task.Run(() => UploadFileAsync(taskInfo));
                tCount++;
            }
        }
    }

    private async Task UploadFileAsync(UploadFileItem taskInfo)
    {
        await taskInfo.UploadAsync(ApiTokenConfig.Instance.Token);
        if (taskInfo.Status == UploadStatus.Success)
        {
            _uploadSuccessAction?.Invoke(taskInfo);
        }
    }

    public void CancelAllAction()
    {
        foreach (var item in _taskList)
        {
            if (item.Status == UploadStatus.Uploading || item.Status == UploadStatus.WaitResponse)
            {
                if (item.CancelSource != null)
                {
                    item.CancelSource.Cancel();
                }
            }
            else if (item.Status == UploadStatus.Pending)
            {
                item.Status = UploadStatus.Stopped;
            }
        }
    }

    public void ResumeAllAction()
    {
        foreach (var item in _taskList)
        {
            if (item.Status == UploadStatus.Stopped || item.Status == UploadStatus.Failed)
            {
                item.Status = UploadStatus.Pending;
            }
        }
    }

    public void ClearSuccessAction()
    {
        var toDeleteList = new List<UploadFileItem>();
        foreach (var item in _taskList)
        {
            if (item.Status == UploadStatus.Success)
            {
                toDeleteList.Add(item);
            }
        }
        if (toDeleteList.Count > 0)
        {
            _taskList.RemoveMany(toDeleteList);
        }
    }

    public void ClearAllAction()
    {
        CancelAllAction();
        _taskList.Clear();
    }
}