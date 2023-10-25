using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Liuguang.NftDisk.Models;
using Liuguang.Storage;
using ReactiveUI;

namespace Liuguang.NftDisk.ViewModels;

/// <summary>
/// 表示一条文件上传任务
/// </summary>
public class UploadFileItem : ViewModelBase
{
    #region Fields
    private readonly long _folderID;
    private readonly IStorageFile _sourceFile;
    private readonly string localFilePath;
    private readonly long fileSize;

    private string cid = string.Empty;
    private string errorMessage = string.Empty;
    private UploadStatus _status = UploadStatus.Pending;
    /// <summary>
    /// 文件块的大小列表
    /// </summary>
    private long[] filePartSizeList;
    /// <summary>
    /// 打包成car的大小列表
    /// </summary>
    private long[] taskPartSizeList;
    /// <summary>
    /// car文件已上传的大小
    /// </summary>
    private long[] taskPartUploadSizeList;
    /// <summary>
    /// 分块状态
    /// </summary>
    private UploadStatus[] taskPartStatusList;
    #endregion

    public long FileSize => fileSize;
    public long UploadSize
    {
        get
        {
            if (_status == UploadStatus.Success)
            {
                return fileSize;
            }
            long t1Size = 0;
            long t2Size = 0;
            for (var i = 0; i < filePartSizeList.Length; i++)
            {
                if (taskPartStatusList[i] == UploadStatus.Success)
                {
                    t1Size += taskPartSizeList[i];
                    t2Size += taskPartSizeList[i];
                }
                else
                {
                    t1Size += taskPartUploadSizeList[i];
                    t2Size += taskPartSizeList[i];
                }
            }
            if (t2Size == 0)
            {
                return 0;
            }
            return fileSize * t1Size / t2Size;
        }
    }

    public string CID
    {
        get => cid;
        private set => this.RaiseAndSetIfChanged(ref cid, value);
    }

    public UploadStatus Status
    {
        get => _status;
        set
        {
            var oldStatus = _status;
            this.RaiseAndSetIfChanged(ref _status, value);
            if (value != oldStatus)
            {
                this.RaisePropertyChanged(nameof(StatusText));
            }
        }

    }

    public string StatusText
    {
        get
        {
            string displayText = "-";
            switch (_status)
            {
                case UploadStatus.Pending:
                    displayText = "排队中";
                    break;
                case UploadStatus.Uploading:
                    displayText = $"上传中({UploadSize}/{FileSize})";
                    break;
                case UploadStatus.WaitResponse:
                    displayText = "等待响应";
                    break;
                case UploadStatus.Success:
                    displayText = "上传成功";
                    break;
                case UploadStatus.Failed:
                    displayText = "上传失败:" + errorMessage;
                    break;
            }
            return displayText;
        }
    }

    public string FileName => _sourceFile.Name;
    public string LocalPath => _sourceFile.Path.LocalPath;

    public long FolderID => _folderID;

    public UploadFileItem(long folderID, IStorageFile file)
    {
        _folderID = folderID;
        _sourceFile = file;
        localFilePath = file.Path.LocalPath;
        var fileInfo = new FileInfo(localFilePath);
        fileSize = fileInfo.Length;
        //分块大小
        var perSize = CarContainer.FilePartMaxSize();
        int partCount;
        if (fileSize <= perSize)
        {
            partCount = 1;
        }
        else
        {
            partCount = (int)(fileSize / perSize);
            if (fileSize % perSize != 0)
            {
                partCount++;
            }
        }
        //初始化
        filePartSizeList = new long[partCount];
        taskPartSizeList = new long[partCount];
        taskPartUploadSizeList = new long[partCount];
        taskPartStatusList = new UploadStatus[partCount];
        for (var i = 0; i < partCount; i++)
        {
            if (i == partCount - 1)
            {
                if (fileSize == 0)
                {
                    filePartSizeList[i] = 0;
                }
                else
                {
                    filePartSizeList[i] = fileSize % perSize;
                    if (filePartSizeList[i] == 0)
                    {
                        filePartSizeList[i] = perSize;
                    }
                }
            }
            else
            {
                filePartSizeList[i] = perSize;
            }
            taskPartSizeList[i] = 0;
            taskPartUploadSizeList[i] = 0;
            taskPartStatusList[i] = UploadStatus.Pending;
        }
    }

    private HttpClient CreateHttpClient(string token)
    {
        var client = new HttpClient()
        {
            BaseAddress = new Uri("https://api.nft.storage"),
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task UploadAsync(string token)
    {
        using var fileStream = await _sourceFile.OpenReadAsync();
        var container = new CarContainer(fileStream);
        var HttpClient = CreateHttpClient(token);
        var partCount = container.TaskCount();
        Status = UploadStatus.Uploading;
        bool allSuccess = true;
        for (var i = 0; i < partCount; i++)
        {
            var partStatus = taskPartStatusList[i];
            if (partStatus == UploadStatus.Success)
            {
                continue;
            }
            taskPartStatusList[i] = UploadStatus.Uploading;
            try
            {
                await UploadPartAsync(HttpClient, container, i);
                taskPartStatusList[i] = UploadStatus.Success;
            }
            catch (Exception ex)
            {
                taskPartStatusList[i] = UploadStatus.Failed;
                errorMessage = ex.Message;
                allSuccess = false;
                break;
            }
        }
        //
        Status = allSuccess ? UploadStatus.Success : UploadStatus.Failed;
    }

    private void UpdateProgress(int taskIndex, long total, long completed)
    {
        taskPartSizeList[taskIndex] = total;
        taskPartUploadSizeList[taskIndex] = completed;
        this.RaisePropertyChanged(nameof(UploadSize));
        if (total == completed)
        {
            Status = UploadStatus.WaitResponse;
        }
    }

    private async Task UploadPartAsync(HttpClient httpClient, CarContainer container, int taskIndex)
    {
        byte[] cidData;
        byte[] carData;
        using (var memoryStream = new MemoryStream())
        {
            cidData = await container.RunCarTaskAsync(memoryStream, taskIndex);
            carData = memoryStream.ToArray();
        }
        CID = CidTool.ToV0String(cidData);
        using var content = new ProgressContent(carData, (total, completed) => UpdateProgress(taskIndex, total, completed));
        content.Headers.ContentType = new("application/car");
        taskPartUploadSizeList[taskIndex] = 0;

        var response = await httpClient.PostAsync("/upload", content);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new Exception("http error, " + response.StatusCode.ToString());
        }
    }
}