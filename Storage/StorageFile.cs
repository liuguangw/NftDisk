namespace Liuguang.Storage;
public class StorageFile
{
    const long ROOT_PATH_ID = 0;
    public long ID { get; set; } = 0;
    public long ParentID { get; set; } = 0;
    public FileType ItemType { get; set; } = FileType.File;
    public string Name { get; set; } = string.Empty;
    public string CID { get; set; } = string.Empty;
    public long Size { get; set; } = 0;
    public long UploadTime { get; set; } = 0;

    public StorageFile(string fileName)
    {
        Name = fileName;
    }

    /// <summary>
    /// 设置时间戳为当前时间
    /// </summary>
    public void SyncTime()
    {
        UploadTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
