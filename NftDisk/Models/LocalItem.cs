using Avalonia.Media.Imaging;
using Liuguang.NftDisk.Common;

namespace Liuguang.NftDisk.Models;

/// <summary>
/// 上传时, 本地选择的文件或者目录
/// </summary>
public class LocalItem : ModelBase
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