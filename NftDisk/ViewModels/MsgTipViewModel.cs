using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Liuguang.NftDisk.Common;
using ReactiveUI;

namespace Liuguang.NftDisk.ViewModels;

public class MsgTipViewModel : ViewModelBase
{
    private bool _showModal = false;
    private bool _isStyleHidden = true;
    private bool _isSuccess = true;
    private string _message = string.Empty;

    public static readonly MsgTipViewModel Instance = new();

    public bool ShowModal
    {
        get => _showModal;
        private set => this.RaiseAndSetIfChanged(ref _showModal, value);
    }

    public bool IsStyleHidden
    {
        get => _isStyleHidden;
        set => this.RaiseAndSetIfChanged(ref _isStyleHidden, value);
    }

    public bool IsSuccess
    {
        get => _isSuccess;
        private set
        {
            var oldValue = _isSuccess;
            this.RaiseAndSetIfChanged(ref _isSuccess, value);
            if (oldValue != value)
            {
                this.RaisePropertyChanged(nameof(IconSource));
                this.RaisePropertyChanged(nameof(TextColor));
                this.RaisePropertyChanged(nameof(BackgroundColor));
                this.RaisePropertyChanged(nameof(BorderColor));
            }
        }
    }

    public string Message
    {
        get => _message;
        private set => this.RaiseAndSetIfChanged(ref _message, value);
    }

    public Bitmap IconSource
    {
        get
        {
            if (_isSuccess)
            {
                return AssetTool.LoadIconImage("success.png");
            }
            else
            {
                return AssetTool.LoadIconImage("error.png");
            }
        }
    }

    public SolidColorBrush TextColor
    {
        get
        {
            Color color;
            if (_isSuccess)
            {
                color = Color.Parse("#0a3622");
            }
            else
            {
                color = Color.Parse("#58151c");
            }
            return new SolidColorBrush(color);
        }
    }

    public SolidColorBrush BackgroundColor
    {
        get
        {
            Color color;
            if (_isSuccess)
            {
                color = Color.Parse("#d1e7dd");
            }
            else
            {
                color = Color.Parse("#f8d7da");
            }
            return new SolidColorBrush(color);
        }
    }

    public SolidColorBrush BorderColor
    {
        get
        {
            Color color;
            if (_isSuccess)
            {
                color = Color.Parse("#a3cfbb");
            }
            else
            {
                color = Color.Parse("#f1aeb5");
            }
            return new SolidColorBrush(color);
        }
    }

    public async void ShowDialog(bool isSuccess, string message)
    {
        IsSuccess = isSuccess;
        Message = message;
        ShowModal = true;
        IsStyleHidden = false;
        await Task.Delay(2500);
        await HideDialogAsync();
    }

    private async Task HideDialogAsync()
    {
        if (!_showModal)
        {
            return;
        }
        IsStyleHidden = true;
        await Task.Delay(200);
        ShowModal = false;
    }

    public async void CloseAction()
    {
        await HideDialogAsync();
    }
}