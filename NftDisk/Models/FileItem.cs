using System;
using Avalonia.Media.Imaging;
using Liuguang.NftDisk.Common;
using Liuguang.Storage;
using ReactiveUI;

namespace Liuguang.NftDisk.Models;

/// <summary>
/// 列表展示的文件或者目录
/// </summary>
public class FileItem : ModelBase
{
    #region Fields
    private long _id = 0;
    private FileType _itemType = FileType.File;
    private string _name = string.Empty;
    private string _cid = string.Empty;
    private long _size = 0;
    private long _uploadTime = 0;
    private bool _selected = false;
    #endregion

    #region Properties
    public long ID => _id;
    public long ParentID { get; set; } = 0;
    public FileType ItemType => _itemType;

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public Bitmap? IconSource
    {
        get
        {
            if (_itemType == FileType.Dir)
            {
                return AssetTool.LoadIconImage("folder.png");
            }
            else if (_itemType == FileType.File)
            {
                return AssetTool.LoadIconImage("file.png");
            }
            return null;
        }
    }

    public string MainActionText
    {
        get
        {
            if (_itemType == FileType.Dir)
            {
                return "打开";
            }
            else if (_itemType == FileType.File)
            {
                return "获取地址";
            }
            return "error";
        }
    }

    public bool CanCopyCid => _itemType == FileType.File;

    public string CID => _cid;

    public string SizeText
    {
        get
        {
            if (_itemType == FileType.Dir)
            {
                return string.Empty;
            }
            return SizeTool.ParseSize(_size);
        }
    }

    public string UploadTimeText => ParseUploadTime(_uploadTime);
    public bool Selected
    {
        get => _selected;
        set => this.RaiseAndSetIfChanged(ref _selected, value);
    }
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
        this._id = id;
        ParentID = parentID;
        this._name = name;
        this._uploadTime = uploadTime;
        this._cid = cid;
        this._size = size;
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
        this._id = id;
        ParentID = parentID;
        _itemType = FileType.Dir;
        this._name = name;
        this._uploadTime = uploadTime;
    }

}