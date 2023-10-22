using System;
using System.Reactive;
using ReactiveUI;

namespace Liuguang.NftDisk.ViewModels;

public class AskStringViewModel : ViewModelBase
{
    private string _title = "标题";
    private string _label = "标签";
    private string _watermark = string.Empty;
    private string _inputText = string.Empty;
    private bool _showModal = false;
    private bool _confirm = false;
    private Action? _completeAction = null;

    public string Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    public string Label
    {
        get => _label;
        set => this.RaiseAndSetIfChanged(ref _label, value);
    }

    public string Watermark
    {
        get => _watermark;
        set => this.RaiseAndSetIfChanged(ref _watermark, value);
    }

    public bool IsNotEmpty => !string.IsNullOrEmpty(_inputText);

    public string InputText
    {
        get => _inputText;
        set
        {
            this.RaiseAndSetIfChanged(ref _inputText, value);
            this.RaisePropertyChanged(nameof(IsNotEmpty));
        }
    }

    public bool ShowModal
    {
        get => _showModal;
        set => this.RaiseAndSetIfChanged(ref _showModal, value);
    }

    public bool Confirm => _confirm;

    public Action CompleteAction { set => _completeAction = value; }
    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public AskStringViewModel()
    {
        var canConfirm = this.WhenAnyValue(item => item.IsNotEmpty);
        ConfirmCommand = ReactiveCommand.Create(ProcessConfirmAction, canConfirm);
        CancelCommand = ReactiveCommand.Create(ProcessCancelAction);
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