using System;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Liuguang.NftDisk.Common;
using Liuguang.Storage;
using ReactiveUI;

namespace Liuguang.NftDisk.ViewModels;

/// <summary>
/// 列表展示的文件或者目录
/// </summary>
public class FileItem : ViewModelBase
{
    public const string ICON_PATH_PREFIX = "avares://NftDisk/Assets/icons";
    #region Fields
    private long id = 0;
    private FileType itemType = FileType.File;
    private string name = string.Empty;
    private string cid = string.Empty;
    private long size = 0;
    private long uploadTime = 0;
    #endregion

    #region Properties
    public long ID => id;
    public long ParentID { get; set; } = 0;
    public FileType ItemType => itemType;

    public string Name
    {
        get => name;
        set => this.RaiseAndSetIfChanged(ref name, value);
    }

    public Bitmap? IconSource
    {
        get
        {
            string imgPath;
            if (itemType == FileType.Dir)
            {
                imgPath = $"{ICON_PATH_PREFIX}/folder.png";
            }
            else
            {
                imgPath = $"{ICON_PATH_PREFIX}/file.png";
            }
            return new Bitmap(AssetLoader.Open(new Uri(imgPath)));
        }
    }

    public string MainActionText
    {
        get
        {
            if (itemType == FileType.Dir)
            {
                return "打开";
            }
            else if (itemType == FileType.File)
            {
                return "获取地址";
            }
            return "error";
        }
    }

    public bool CanCopyCid => itemType == FileType.File;

    public string CID => cid;

    public string SizeText
    {
        get
        {
            if (itemType == FileType.Dir)
            {
                return string.Empty;
            }
            return SizeTool.ParseSize(size);
        }
    }

    public string UploadTimeText => ParseUploadTime(uploadTime);
    #endregion

    private static string ParseUploadTime(long uploadTime)
    {
        var timeOffset = DateTimeOffset.FromUnixTimeSeconds(uploadTime);
        var localTime = timeOffset.ToLocalTime();
        return localTime.ToString("yyyy-MM-dd HH:mm:ss");
    }

    /// <summary>
    /// 文件
    /// </summary>
    /// <param name="id"></param>
    /// <param name="parentID"></param>
    /// <param name="name"></param>
    /// <param name="uploadTime"></param>
    /// <param name="cid"></param>
    /// <param name="size"></param>
    public FileItem(long id, long parentID, string name, long uploadTime, string cid, long size)
    {
        this.id = id;
        ParentID = parentID;
        this.name = name;
        this.uploadTime = uploadTime;
        this.cid = cid;
        this.size = size;
    }

    /// <summary>
    /// 文件夹
    /// </summary>
    /// <param name="id"></param>
    /// <param name="parentID"></param>
    /// <param name="name"></param>
    /// <param name="uploadTime"></param>
    public FileItem(long id, long parentID, string name, long uploadTime)
    {
        this.id = id;
        ParentID = parentID;
        itemType = FileType.Dir;
        this.name = name;
        this.uploadTime = uploadTime;
    }

}