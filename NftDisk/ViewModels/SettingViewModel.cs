using System;
using System.Reactive;
using Liuguang.NftDisk.Config;
using ReactiveUI;

namespace Liuguang.NftDisk.ViewModels;

public class SettingViewModel : ViewModelBase
{
    private string _tokenText = string.Empty;
    private string _gatewayText = string.Empty;
    private bool _showModal = false;
    private Action? _completeAction = null;

    public bool IsNotEmpty => !(string.IsNullOrEmpty(_tokenText) || string.IsNullOrEmpty(_gatewayText));

    public string TokenText
    {
        get => _tokenText;
        set
        {
            this.RaiseAndSetIfChanged(ref _tokenText, value);
            this.RaisePropertyChanged(nameof(IsNotEmpty));
        }
    }
    public string GatewayText
    {
        get => _gatewayText;
        set
        {
            this.RaiseAndSetIfChanged(ref _gatewayText, value);
            this.RaisePropertyChanged(nameof(IsNotEmpty));
        }
    }

    public bool ShowModal
    {
        get => _showModal;
        private set => this.RaiseAndSetIfChanged(ref _showModal, value);
    }

    public Action CompleteAction { set => _completeAction = value; }
    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public SettingViewModel()
    {
        var canConfirm = this.WhenAnyValue(item => item.IsNotEmpty);
        ConfirmCommand = ReactiveCommand.Create(ProcessConfirmAction, canConfirm);
        CancelCommand = ReactiveCommand.Create(ProcessCancelAction);
    }

    private async void ProcessConfirmAction()
    {
        ShowModal = false;
        _completeAction?.Invoke();
        ApiTokenConfig.Instance.Token = _tokenText;
        GatewayConfig.Instance.AddressList.Clear();
        var rows = _gatewayText.Trim().Split('\n');
        foreach (var content in rows)
        {
            var content1 = content.Trim().TrimEnd('/');
            if (!string.IsNullOrEmpty(content1))
            {
                GatewayConfig.Instance.AddressList.Add(content1);
            }
        }
        try
        {
            await ApiTokenConfig.SaveAsync(ApiTokenConfig.DEFAULT_PATH);
            await GatewayConfig.SaveAsync(GatewayConfig.DEFAULT_PATH);
            MsgTipViewModel.Instance.ShowDialog(true, "保存配置成功");
        }
        catch (Exception ex)
        {
            MsgTipViewModel.Instance.ShowDialog(false, "保存配置失败, " + ex.Message);
        }
    }

    private void ProcessCancelAction()
    {
        ShowModal = false;
        _completeAction?.Invoke();
    }

    public void ShowDialog()
    {
        ShowModal = true;
        TokenText = ApiTokenConfig.Instance.Token;
        if (GatewayConfig.Instance.AddressList.Count > 0)
        {
            GatewayText = string.Join('\n', GatewayConfig.Instance.AddressList);
        }
        else
        {
            GatewayText = string.Empty;
        }
    }
}