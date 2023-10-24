using System;
using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;

namespace Liuguang.NftDisk.ViewModels;
public class UploadListViewModel : ViewModelBase
{
    private readonly ObservableCollection<UploadFileItem> _taskList = new();
    public ObservableCollection<UploadFileItem> TaskList => _taskList;
    private bool _showModal = false;
    private bool _confirm = false;
    private Action? _completeAction = null;

    public bool ShowModal
    {
        get => _showModal;
        set => this.RaiseAndSetIfChanged(ref _showModal, value);
    }

    public bool Confirm => _confirm;

    public Action CompleteAction { set => _completeAction = value; }
    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public UploadListViewModel()
    {
        ConfirmCommand = ReactiveCommand.Create(ProcessConfirmAction);
        CancelCommand = ReactiveCommand.Create(ProcessCancelAction);
    }

    private void ProcessConfirmAction()
    {
        _confirm = true;
        ShowModal = false;
        _completeAction?.Invoke();
    }

    private void ProcessCancelAction()
    {
        _confirm = false;
        ShowModal = false;
        _completeAction?.Invoke();
    }
}