using System;
using System.Collections.ObjectModel;
using System.Reactive;
using Liuguang.NftDisk.Models;
using ReactiveUI;

namespace Liuguang.NftDisk.ViewModels;

public class AskUploadViewModel : ViewModelBase
{
    private readonly ObservableCollection<LocalItem> _localItemList = new();
    private bool _showModal = false;
    private bool _confirm = false;
    private Action? _completeAction = null;

    public ObservableCollection<LocalItem> LocalItemList => _localItemList;

    public bool ShowModal
    {
        get => _showModal;
        set => this.RaiseAndSetIfChanged(ref _showModal, value);
    }

    public bool Confirm => _confirm;

    public Action CompleteAction { set => _completeAction = value; }
    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public AskUploadViewModel()
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