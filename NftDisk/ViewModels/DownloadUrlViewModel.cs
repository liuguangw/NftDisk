using System;
using System.Text;
using System.Web;
using Liuguang.NftDisk.Config;
using ReactiveUI;

namespace Liuguang.NftDisk.ViewModels;

public class DownloadUrlViewModel : ViewModelBase
{
    private string _ipfsUrl = string.Empty;
    private string _gatewayUrls = string.Empty;
    private bool _showModal = false;
    private Action? _completeAction = null;

    public string IpfsUrl
    {
        get => _ipfsUrl;
        set => this.RaiseAndSetIfChanged(ref _ipfsUrl, value);
    }
    public string GatewayUrls
    {
        get => _gatewayUrls;
        set => this.RaiseAndSetIfChanged(ref _gatewayUrls, value);
    }

    public bool ShowModal
    {
        get => _showModal;
        private set => this.RaiseAndSetIfChanged(ref _showModal, value);
    }

    public Action CompleteAction { set => _completeAction = value; }

    public void CloseAction()
    {
        ShowModal = false;
        _completeAction?.Invoke();
    }

    public void ShowDialog(string cid, string filename)
    {
        ShowModal = true;
        var encodedName = HttpUtility.UrlEncode(filename);
        var query = string.Empty;
        if (!string.IsNullOrEmpty(encodedName))
        {
            query = $"?filename={encodedName}";
        }
        var ipfsPart = $"{cid}/{query}";
        IpfsUrl = "ipfs://" + ipfsPart;
        var gatewayUrls = new StringBuilder();
        int i = 0;
        foreach (var gatewayPrefix in GatewayConfig.Instance.AddressList)
        {
            var gatewayUrl = $"{gatewayPrefix}/{ipfsPart}";
            if (i == 0)
            {
                gatewayUrls.Append(gatewayUrl);
            }
            else
            {
                gatewayUrls.AppendFormat("\n{0}", gatewayUrl);
            }
            i++;
        }
        GatewayUrls = gatewayUrls.ToString();
    }
}