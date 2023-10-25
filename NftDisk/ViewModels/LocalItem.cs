using System;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Liuguang.NftDisk.Common;
using Liuguang.NftDisk.ViewModels;

/// <summary>
/// 上传时, 本地选择的文件或者目录
/// </summary>
public class LocalItem : ViewModelBase
{
    private readonly string _name;
    private readonly bool _isFile;

    /// <summary>
    /// 名称
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// 图标
    /// </summary>
    public Bitmap IconSource
    {
        get
        {
            if (_isFile)
            {
                return AssetTool.LoadIconImage("file.png");
            }
            else
            {
                return AssetTool.LoadIconImage("folder.png");
            }
        }
    }

    public LocalItem(string name, bool isFile)
    {
        _name = name;
        _isFile = isFile;
    }
}