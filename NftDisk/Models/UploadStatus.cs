namespace Liuguang.NftDisk.Models;

public enum UploadStatus
{
    //排队中
    Pending,
    //正在上传
    Uploading,
    //上传成功
    Success,
    //上传失败
    Failed,
    //被手动停止
    Stopped,
}