using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
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
    private string _token = string.Empty;
    private bool _showModal = false;
    private bool _confirm = false;
    private Action? _completeAction = null;
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
        set => this.RaiseAndSetIfChanged(ref _showModal, value);
    }

    public bool Confirm => _confirm;

    public Action CompleteAction { set => _completeAction = value; }
    public Action<UploadFileItem> UploadSuccessAction { set => _uploadSuccessAction = value; }
    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public UploadListViewModel()
    {
        ConfirmCommand = ReactiveCommand.Create(ProcessConfirmAction);
        CancelCommand = ReactiveCommand.Create(ProcessCancelAction);
        _token = "123456";
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
                //从激活的任务列表中删除已经完成的任务
                if (taskInfo.Status == UploadStatus.Success || taskInfo.Status == UploadStatus.Failed)
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
                foreach (var item in toRemoveList)
                {
                    _activeTaskList.Remove(item);
                }
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
        await taskInfo.UploadAsync(_token);
        if (taskInfo.Status == UploadStatus.Success)
        {
            _uploadSuccessAction?.Invoke(taskInfo);
        }
    }
}