using System;
using Liuguang.Storage;
using ReactiveUI;

namespace Liuguang.NftDisk.ViewModels;

public class FileItem : ViewModelBase
{
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
    public string ItemTypeText
    {
        get
        {
            if (itemType == FileType.Dir)
            {
                return "文件夹";
            }
            else if (itemType == FileType.File)
            {
                return "文件";
            }
            return "-";
        }
    }
    public string Name
    {
        get => name;
        set => this.RaiseAndSetIfChanged(ref name, value);
    }
    public string CID => cid;

    public string SizeText
    {
        get
        {
            if (itemType == FileType.Dir)
            {
                return string.Empty;
            }
            return ParseSize(size);
        }
    }

    public string UploadTimeText => ParseUploadTime(uploadTime);
    #endregion

    private static string ParseSize(long sizeValue)
    {
        const long KB = 1024;
        const long MB = 1024 * KB;
        const long GB = 1024 * MB;
        if (sizeValue < 0)
        {
            //出错了
            return "?";
        }
        else if (sizeValue >= 0 && sizeValue < KB)
        {
            return $"{sizeValue} B";
        }
        else if (sizeValue >= KB && sizeValue < MB)
        {
            double v = (double)sizeValue / KB;
            return $"{v:N2} KB";
        }
        else if (sizeValue >= MB && sizeValue < GB)
        {
            double v = (double)sizeValue / MB;
            return $"{v:N2} MB";
        }
        else
        {
            double v = (double)sizeValue / GB;
            return $"{v:N2} GB";
        }
    }

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