using System;
using System.Reactive;
using ReactiveUI;

namespace Liuguang.NftDisk.ViewModels;

public class ConfirmViewModel : ViewModelBase
{
    private bool _showModal = false;
    private bool _confirm = false;
    private Action? _completeAction = null;

    private string _title = "操作提示";
    private string _content = "操作说明";

    public bool ShowModal
    {
        get => _showModal;
        private set => this.RaiseAndSetIfChanged(ref _showModal, value);
    }

    public bool Confirm => _confirm;

    public Action CompleteAction { set => _completeAction = value; }

    public string Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    public string Content
    {
        get => _content;
        set => this.RaiseAndSetIfChanged(ref _content, value);
    }
    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public ConfirmViewModel()
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

    public void ShowDialog(string title, string content)
    {
        Title = title;
        Content = content;
        ShowModal = true;
    }
}