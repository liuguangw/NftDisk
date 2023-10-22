using System.Security.Cryptography;
using Google.Protobuf;
using Liuguang.Storage.Pb;
using UnixFsData = Liuguang.Storage.Pb.Data;

namespace Liuguang.Storage;

public class CarContainer
{
    private readonly Stream stream;
    private readonly long fileSize;
    /// <summary>
    /// 每个node包含的文件字节的最大值(256KB)
    /// </summary>
    const long NODE_MAX_SIZE = 262144;

    /// <summary>
    /// 每个link最多直接包含多少个256KB的子link,
    /// 
    /// size = 45613056(43.5MB)
    /// </summary>
    const long LINK_NODE_MAX_COUNT = 174;

    /// <summary>
    /// 每次任务上传的文件内容大小
    /// 43.5MB *2 = 87MB
    /// </summary>
    const int TASK_LINK_MAX_COUNT = 2;

    public CarContainer(Stream stream)
    {
        this.stream = stream;
        fileSize = stream.Length;
    }

    public static long FilePartMaxSize()
    {
        return NODE_MAX_SIZE * LINK_NODE_MAX_COUNT * TASK_LINK_MAX_COUNT;
    }

    private long AllNodeCount()
    {
        if (fileSize == 0)
        {
            return 1;
        }
        var count = fileSize / NODE_MAX_SIZE;
        if (fileSize % NODE_MAX_SIZE != 0)
        {
            count++;
        }
        return count;
    }

    private long NodeContentLength(long nodeIndex)
    {
        var offset = nodeIndex * NODE_MAX_SIZE;
        var nodeSize = NODE_MAX_SIZE;
        var leftSize = fileSize - offset;
        if (nodeSize > leftSize)
        {
            nodeSize = leftSize;
        }
        return nodeSize;
    }

    private async Task<PBNode> ReadNodeAsync(long nodeIndex)
    {
        var offset = nodeIndex * NODE_MAX_SIZE;
        var nodeSize = NodeContentLength(nodeIndex);
        var buffer = new byte[nodeSize];
        //seek
        stream.Seek(offset, SeekOrigin.Begin);
        await stream.ReadAsync(buffer);
        var unixfsData = new UnixFsData()
        {
            Type = UnixFsData.Types.DataType.File,
            Filesize = (ulong)nodeSize,
            Data_ = ByteString.CopyFrom(buffer),
        };
        var data = unixfsData.ToByteArray();
        return new PBNode()
        {
            Data = ByteString.CopyFrom(data),
        };
    }

    private static (byte[], byte[]) PackNode(PBNode pbNode)
    {
        var packData = pbNode.ToByteArray();
        var cid = CalcCid(packData);
        return (cid, packData);
    }

    private static byte[] CalcCid(byte[] bytes)
    {
        var cidData = new byte[2 + 32];
        cidData[0] = 0x12;
        cidData[1] = 0x20;
        var hashData = SHA256.HashData(bytes);
        hashData.CopyTo(cidData, 2);
        return cidData;
    }

    public int TaskCount()
    {
        var perSize = FilePartMaxSize();
        if (fileSize <= perSize)
        {
            return 1;
        }
        var count = fileSize / perSize;
        if (fileSize % perSize != 0)
        {
            count++;
        }
        return (int)count;
    }

    private async Task WriteCarHeadAsync(Stream outputStream, byte[] cid)
    {
        var part = new byte[]{
            0x38,//总长度
            0xA2,//map(2)
            0x65,//text(5)
            0x72,0x6F,0x6F,0x74,0x73,//roots
            0x81,//array(1)
            0xD8, 0x2A,//tag(42)
            0x58, 0x23,//bytes(35)
            0x0,//cid前缀
        };
        await outputStream.WriteAsync(part);
        await outputStream.WriteAsync(cid);
        var part1 = new byte[]{
            0x67,//text(7),
            0x76,0x65,0x72,0x73,0x69,0x6F,0x6E,//version
            0x1, //unsigned(1)
        };
        await outputStream.WriteAsync(part1);
    }

    public async Task<byte[]> RunCarTaskAsync(Stream outputStream, int taskIndex)
    {
        if (fileSize <= NODE_MAX_SIZE)
        {
            // [0 - 256KB]
            return await RunCarTaskV0Async(outputStream);
        }
        else if (fileSize > NODE_MAX_SIZE && fileSize <= (LINK_NODE_MAX_COUNT * NODE_MAX_SIZE))
        {
            // (256KB - 43.5MB]
            return await RunCarTaskV1Async(outputStream);
        }
        else if (fileSize > (LINK_NODE_MAX_COUNT * NODE_MAX_SIZE) && fileSize <= (TASK_LINK_MAX_COUNT * LINK_NODE_MAX_COUNT * NODE_MAX_SIZE))
        {
            // (43.5MB - 87MB]
            return await RunCarTaskV2Async(outputStream);
        }
        else if (fileSize > TASK_LINK_MAX_COUNT * LINK_NODE_MAX_COUNT * NODE_MAX_SIZE)
        {
            // (87MB - ?]
            return await RunCarTaskV3Async(outputStream, taskIndex);
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private async Task<(byte[], int)> WritePbNodeAsync(Stream outputStream, PBNode pbNode, bool writeHead = false)
    {
        var (cid, data) = PackNode(pbNode);
        if (writeHead)
        {
            await WriteCarHeadAsync(outputStream, cid);
        }
        var fLength = cid.Length + data.Length;
        //varint
        var varintData = Varint.ToUvarint((ulong)fLength);
        await outputStream.WriteAsync(varintData);
        await outputStream.WriteAsync(cid);
        await outputStream.WriteAsync(data);
        return (cid, data.Length);
    }

    /// <summary>
    /// 处理 <=256KB 大小的文件
    /// </summary>
    /// <param name="outputStream"></param>
    /// <returns></returns>
    private async Task<byte[]> RunCarTaskV0Async(Stream outputStream)
    {
        var pbNode = await ReadNodeAsync(0);
        var result = await WritePbNodeAsync(outputStream, pbNode, true);
        return result.Item1;
    }

    /// <summary>
    /// 写入一个link，每个link最多包含174个子元素(43.5MB)
    /// </summary>
    /// <param name="outputStream"></param>
    /// <param name="linkIndex">link序号</param>
    /// <param name="writeHead">是否写入car head头</param>
    /// <returns></returns>
    private async Task<(byte[], long)> WriteLinkAsync(Stream outputStream, int linkIndex, bool writeHead)
    {
        var nodeIndex = linkIndex * LINK_NODE_MAX_COUNT;
        var nodeTotalCount = AllNodeCount();
        var leftNodeCount = nodeTotalCount - nodeIndex;
        var loopCount = Math.Min(LINK_NODE_MAX_COUNT, leftNodeCount);
        using var memStream = new MemoryStream();
        var linkPbNode = new PBNode();
        var linkUnixFsData = new UnixFsData()
        {
            Type = UnixFsData.Types.DataType.File,
            Filesize = 0,
        };
        long totalDataLength = 0;
        for (var i = 0; i < loopCount; i++)
        {
            //Console.WriteLine("node:{0}",nodeIndex);
            var pbNode = await ReadNodeAsync(nodeIndex);
            var (cid, dataLength) = await WritePbNodeAsync(memStream, pbNode);
            totalDataLength += dataLength;
            //add links
            linkPbNode.Links.Add(new PBLink()
            {
                Hash = ByteString.CopyFrom(cid),
                Tsize = (ulong)dataLength,
                Name = string.Empty,
            });
            var nodeSize = (ulong)NodeContentLength(nodeIndex);
            linkUnixFsData.Filesize += nodeSize;
            linkUnixFsData.Blocksizes.Add(nodeSize);
            nodeIndex++;
        }
        linkPbNode.Data = linkUnixFsData.ToByteString();
        var (linkCid, linkDataLength) = await WritePbNodeAsync(outputStream, linkPbNode, writeHead);
        totalDataLength += linkDataLength;
        memStream.Seek(0, SeekOrigin.Begin);
        await memStream.CopyToAsync(outputStream);
        await outputStream.FlushAsync();
        return (linkCid, totalDataLength);
    }

    /// <summary>
    /// 处理 (256KB - 43.5MB] 大小的文件
    /// </summary>
    /// <param name="outputStream"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private async Task<byte[]> RunCarTaskV1Async(Stream outputStream)
    {
        var result = await WriteLinkAsync(outputStream, 0, true);
        return result.Item1;
    }

    /// <summary>
    /// 处理 (43.5MB - 87MB] 大小的文件
    /// </summary>
    /// <param name="outputStream"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private async Task<byte[]> RunCarTaskV2Async(Stream outputStream)
    {
        using var memStream = new MemoryStream();
        var (cid1, dataLength1) = await WriteLinkAsync(memStream, 0, false);
        var (cid2, dataLength2) = await WriteLinkAsync(memStream, 1, false);
        var contentLength1 = NODE_MAX_SIZE * LINK_NODE_MAX_COUNT;
        long contentLength2 = contentLength1;
        if (contentLength1 * 2 > fileSize)
        {
            contentLength2 = fileSize - contentLength1;
        }
        var rootPbNode = new PBNode();
        var rootUnixFsData = new UnixFsData()
        {
            Type = UnixFsData.Types.DataType.File,
            Filesize = (ulong)fileSize,
        };
        //add links
        rootPbNode.Links.Add(new PBLink()
        {
            Hash = ByteString.CopyFrom(cid1),
            Tsize = (ulong)dataLength1,
            Name = string.Empty,
        });
        rootPbNode.Links.Add(new PBLink()
        {
            Hash = ByteString.CopyFrom(cid2),
            Tsize = (ulong)dataLength2,
            Name = string.Empty,
        });
        rootUnixFsData.Blocksizes.Add((ulong)contentLength1);
        rootUnixFsData.Blocksizes.Add((ulong)contentLength2);
        //
        rootPbNode.Data = rootUnixFsData.ToByteString();
        var result = await WritePbNodeAsync(outputStream, rootPbNode, true);
        memStream.Seek(0, SeekOrigin.Begin);
        await memStream.CopyToAsync(outputStream);
        await outputStream.FlushAsync();
        return result.Item1;
    }
    /// <summary>
    /// 处理大于 87MB 大小的文件
    /// </summary>
    /// <param name="outputStream"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private async Task<byte[]> RunCarTaskV3Async(Stream outputStream, int taskIndex)
    {
        var rootPbNode = new PBNode();
        var rootUnixFsData = new UnixFsData()
        {
            Type = UnixFsData.Types.DataType.File,
            Filesize = (ulong)fileSize,
        };
        using var sStream = new MemoryStream();
        //
        var nodeTotalCount = AllNodeCount();
        var taskTotalCount = TaskCount();
        var linkIndex = 0;
        //
        var normalLinkContentSize = (ulong)LINK_NODE_MAX_COUNT * NODE_MAX_SIZE;
        var lastContentSize = (ulong)fileSize % normalLinkContentSize;
        for (var i = 0; i < taskTotalCount; i++)
        {
            using (var memStream = new MemoryStream())
            {
                for (var j = 0; j < TASK_LINK_MAX_COUNT; j++)
                {

                    var (cid, dataLength) = await WriteLinkAsync(memStream, linkIndex, false);
                    linkIndex++;
                    nodeTotalCount -= LINK_NODE_MAX_COUNT;
                    if (nodeTotalCount <= 0)
                    {
                        rootUnixFsData.Blocksizes.Add(lastContentSize);
                    }
                    else
                    {
                        rootUnixFsData.Blocksizes.Add(normalLinkContentSize);
                    }
                    //add links
                    rootPbNode.Links.Add(new PBLink()
                    {
                        Hash = ByteString.CopyFrom(cid),
                        Tsize = (ulong)dataLength,
                        Name = string.Empty,
                    });
                    if (nodeTotalCount <= 0)
                    {
                        break;
                    }
                }
                if (i == taskIndex)
                {
                    memStream.Seek(0, SeekOrigin.Begin);
                    await memStream.CopyToAsync(sStream);
                }
            }
        }
        //
        rootPbNode.Data = rootUnixFsData.ToByteString();
        var result = await WritePbNodeAsync(outputStream, rootPbNode, true);
        sStream.Seek(0, SeekOrigin.Begin);
        await sStream.CopyToAsync(outputStream);
        await outputStream.FlushAsync();
        return result.Item1;
    }
}