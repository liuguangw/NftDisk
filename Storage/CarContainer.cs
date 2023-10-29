using System.Security.Cryptography;
using Google.Protobuf;
using Liuguang.Storage.Pb;
using UnixFsData = Liuguang.Storage.Pb.Data;

namespace Liuguang.Storage;

public class CarContainer
{
    private readonly Stream _stream;
    private readonly long _fileSize;
    private PBNode? _rootPBNode = null;
    /// <summary>
    /// 每个node0包含的文件字节的最大值(256KB)
    /// </summary>
    const long NODE0_MAX_SIZE = 262144;

    /// <summary>
    /// 每个node1最多直接包含多少个node0
    /// 
    /// size = 45613056(43.5MB)
    /// </summary>
    const long NODE1_MAX_CHILDREN_COUNT = 174;

    private long _taskNode0MaxCount = 174;
    public long TaskNode0MaxCount
    {
        get => _taskNode0MaxCount;
        set
        {
            _taskNode0MaxCount = (value >= NODE1_MAX_CHILDREN_COUNT) ? value : NODE1_MAX_CHILDREN_COUNT;
        }
    }

    public CarContainer(Stream stream)
    {
        _stream = stream;
        _fileSize = stream.Length;
    }

    public static long FilePartMaxSize(long node0MaxCount){
        
        return NODE0_MAX_SIZE * node0MaxCount;
    }

    private static async Task<byte[]> ReadBufferAsync(Stream stream, long sizeCount, CancellationToken cancellationToken)
    {
        var buffer = new byte[sizeCount];
        long offset = 0;
        while (offset < sizeCount)
        {
            var count = sizeCount - offset;
            offset += await stream.ReadAsync(buffer.AsMemory((int)offset, (int)count), cancellationToken);
        }
        return buffer;
    }

    private static async Task<PBNode> ReadNode0Async(Stream stream, long node0Size, CancellationToken cancellationToken)
    {
        var fileData = await ReadBufferAsync(stream, node0Size, cancellationToken);
        var unixfsData = new UnixFsData()
        {
            Type = UnixFsData.Types.DataType.File,
            Filesize = (ulong)node0Size,
            Data_ = ByteString.CopyFrom(fileData),
        };
        var data = unixfsData.ToByteArray();
        return new PBNode()
        {
            Data = ByteString.CopyFrom(data),
        };
    }

    private static async Task<PBNode> ReadNode1Async(Stream stream, long node1Size, Stream? node0OutStream, CancellationToken cancellationToken)
    {
        var loopCount = node1Size / NODE0_MAX_SIZE;
        if (node1Size % NODE0_MAX_SIZE != 0)
        {
            loopCount++;
        }
        if (loopCount > NODE1_MAX_CHILDREN_COUNT)
        {
            throw new Exception($"invalid node1Size {node1Size}");
        }
        var node1Root = new PBNode();
        var node1UnixfsData = new UnixFsData()
        {
            Type = UnixFsData.Types.DataType.File,
            Filesize = (ulong)node1Size,
        };

        var tFileSize = node1Size;
        while (tFileSize > 0)
        {
            var nodeSize = Math.Min(tFileSize, NODE0_MAX_SIZE);
            tFileSize -= nodeSize;

            var node0 = await ReadNode0Async(stream, nodeSize, cancellationToken);
            node1UnixfsData.Blocksizes.Add((ulong)nodeSize);
            byte[] node0Cid;
            int node0Length;
            if (node0OutStream is null)
            {
                (node0Cid, node0Length) = PackPBNode(node0);
            }
            else
            {
                //避免重复计算
                (node0Cid, node0Length) = await WritePBNodeAsync(node0OutStream, node0, cancellationToken);
            }
            node1Root.Links.Add(new PBLink()
            {
                Hash = ByteString.CopyFrom(node0Cid),
                Name = string.Empty,
                Tsize = (ulong)node0Length,
            });
        }
        node1Root.Data = node1UnixfsData.ToByteString();
        return node1Root;
    }

    private static (byte[], int) PackPBNode(PBNode pbNode)
    {
        var pbNodeRaw = pbNode.ToByteArray();
        var cidData = CalcCid(pbNodeRaw);
        return (cidData, pbNodeRaw.Length);
    }

    private static async Task<(byte[], int)> WritePBNodeAsync(Stream outputStream, PBNode pbNode, CancellationToken cancellationToken)
    {
        var pbNodeRaw = pbNode.ToByteArray();
        var cidData = CalcCid(pbNodeRaw);
        //varint
        var fullLength = cidData.Length + pbNodeRaw.Length;
        var varintData = Varint.ToUvarint((ulong)fullLength);
        await outputStream.WriteAsync(varintData, cancellationToken);
        await outputStream.WriteAsync(cidData, cancellationToken);
        await outputStream.WriteAsync(pbNodeRaw, cancellationToken);
        return (cidData, pbNodeRaw.Length);
    }

    private static byte[] CalcCid(byte[] bytes)
    {
        var hashData = SHA256.HashData(bytes);
        var cidData = new byte[2 + 32];
        cidData[0] = 0x12;
        cidData[1] = 0x20;
        hashData.CopyTo(cidData, 2);
        return cidData;
    }

    private async Task LoadRootPBNodeAsync(CancellationToken cancellationToken)
    {
        _rootPBNode = await LoadRootPBNodeAsync(_stream, _fileSize, cancellationToken);
    }

    private static async Task<PBNode> LoadRootPBNodeAsync(Stream stream, long fileSize, CancellationToken cancellationToken)
    {
        if (fileSize <= NODE0_MAX_SIZE)
        {
            return await ReadNode0Async(stream, fileSize, cancellationToken);
        }
        var node1MaxSize = NODE0_MAX_SIZE * NODE1_MAX_CHILDREN_COUNT;
        if (fileSize <= node1MaxSize)
        {
            return await ReadNode1Async(stream, fileSize, null, cancellationToken);
        }
        //
        var node2Root = new PBNode();
        var node2UnixfsData = new UnixFsData()
        {
            Type = UnixFsData.Types.DataType.File,
            Filesize = (ulong)fileSize,
        };
        var tFileSize = fileSize;
        while (tFileSize > 0)
        {
            var nodeSize = Math.Min(tFileSize, node1MaxSize);
            tFileSize -= nodeSize;
            PBNode nodex;
            if (nodeSize <= NODE0_MAX_SIZE)
            {
                nodex = await ReadNode0Async(stream, nodeSize, cancellationToken);
            }
            else
            {
                nodex = await ReadNode1Async(stream, nodeSize, null, cancellationToken);
            }
            node2UnixfsData.Blocksizes.Add((ulong)nodeSize);
            var (nodexCid, nodexLength) = PackPBNode(nodex);
            ulong linkTSize = (ulong)nodexLength;
            if (nodex.Links.Count > 0)
            {
                foreach (var linkItem in nodex.Links)
                {
                    linkTSize += linkItem.Tsize;
                }
            }
            node2Root.Links.Add(new PBLink()
            {
                Hash = ByteString.CopyFrom(nodexCid),
                Name = string.Empty,
                Tsize = linkTSize,
            });
        }
        node2Root.Data = node2UnixfsData.ToByteString();
        return node2Root;
    }

    private static async Task WriteCarHeadAsync(Stream outputStream, byte[] cid, CancellationToken cancellationToken)
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
        await outputStream.WriteAsync(part, cancellationToken);
        await outputStream.WriteAsync(cid, cancellationToken);
        var part1 = new byte[]{
            0x67,//text(7),
            0x76,0x65,0x72,0x73,0x69,0x6F,0x6E,//version
            0x1, //unsigned(1)
        };
        await outputStream.WriteAsync(part1, cancellationToken);
    }

    public async Task<byte[]> WriteCarAsync(Stream outputStream, CancellationToken cancellationToken = default)
    {
        await LoadRootPBNodeAsync(cancellationToken);
        var rootPbNode = _rootPBNode!;
        var (cidData, _) = PackPBNode(rootPbNode);
        await WriteCarHeadAsync(outputStream, cidData, cancellationToken);
        await WritePBNodeAsync(outputStream, rootPbNode, cancellationToken);
        if (_fileSize <= NODE0_MAX_SIZE)
        {
            return cidData;
        }
        var node1MaxSize = NODE0_MAX_SIZE * NODE1_MAX_CHILDREN_COUNT;
        if (_fileSize <= node1MaxSize)
        {
            _stream.Seek(0, SeekOrigin.Begin);
            await WriteNode0ListAsync(_stream, _fileSize, outputStream, cancellationToken);
            return cidData;
        }
        _stream.Seek(0, SeekOrigin.Begin);
        var tFileSize = _fileSize;
        while (tFileSize > 0)
        {
            var nodeSize = Math.Min(tFileSize, node1MaxSize);
            tFileSize -= nodeSize;
            if (nodeSize <= NODE0_MAX_SIZE)
            {
                var node0 = await ReadNode0Async(_stream, nodeSize, cancellationToken);
                await WritePBNodeAsync(outputStream, node0, cancellationToken);
            }
            else
            {
                using (var memStream = new MemoryStream())
                {
                    var node1 = await ReadNode1Async(_stream, nodeSize, memStream, cancellationToken);
                    await WritePBNodeAsync(outputStream, node1, cancellationToken);
                    memStream.Seek(0, SeekOrigin.Begin);
                    await memStream.CopyToAsync(outputStream, cancellationToken);
                }
            }
        }
        return cidData;
    }

    private static async Task WriteNode0ListAsync(Stream stream, long totalSize, Stream outputStream, CancellationToken cancellationToken)
    {
        var loopCount = totalSize / NODE0_MAX_SIZE;
        if (totalSize % NODE0_MAX_SIZE != 0)
        {
            loopCount++;
        }
        if (loopCount > NODE1_MAX_CHILDREN_COUNT)
        {
            throw new Exception($"invalid node1Size {totalSize}");
        }
        var tFileSize = totalSize;
        while (tFileSize > 0)
        {
            var nodeSize = Math.Min(tFileSize, NODE0_MAX_SIZE);
            tFileSize -= nodeSize;
            var node0 = await ReadNode0Async(stream, nodeSize, cancellationToken);
            await WritePBNodeAsync(outputStream, node0, cancellationToken);
        }
    }
    public int TaskCount()
    {
        var perSize = FilePartMaxSize(_taskNode0MaxCount);
        if (_fileSize <= perSize)
        {
            return 1;
        }
        var count = _fileSize / perSize;
        if (_fileSize % perSize != 0)
        {
            count++;
        }
        return (int)count;
    }

    public async Task<byte[]> WriteCarAsync(Stream outputStream, int taskIndex, CancellationToken cancellationToken = default)
    {
        var partSize = FilePartMaxSize(_taskNode0MaxCount);
        if (_fileSize <= partSize)
        {
            //不需要分片
            return await WriteCarAsync(outputStream, cancellationToken);
        }
        if (_rootPBNode is null)
        {
            await LoadRootPBNodeAsync(cancellationToken);
        }
        var rootPbNode = _rootPBNode!;
        var (cidData, _) = PackPBNode(rootPbNode);
        await WriteCarHeadAsync(outputStream, cidData, cancellationToken);
        await WritePBNodeAsync(outputStream, rootPbNode, cancellationToken);
        var processedSize = partSize * taskIndex;
        if (_stream.Position != processedSize)
        {
            _stream.Seek(processedSize, SeekOrigin.Begin);
        }
        //文件剩余部分的大小
        var fileRmainSize = _fileSize - processedSize;
        //写入大小计数
        long nodeFileSize = 0;
        var node1MaxSize = NODE0_MAX_SIZE * NODE1_MAX_CHILDREN_COUNT;
        if (processedSize % node1MaxSize != 0)
        {
            var lastNode1Offset = processedSize / node1MaxSize * node1MaxSize;
            //被截断的左边部分大小
            var part0Size = processedSize - lastNode1Offset;
            _stream.Seek(-part0Size, SeekOrigin.Current);
            //node1的总大小
            var lastNode1Size = Math.Min(fileRmainSize + part0Size, node1MaxSize);
            //右边部分的大小
            nodeFileSize = lastNode1Size - part0Size;
            fileRmainSize -= nodeFileSize;
            var lastNode1 = await ReadNode1Async(_stream, lastNode1Size, null, cancellationToken);
            //写入上一个node1的定义
            await WritePBNodeAsync(outputStream, lastNode1, cancellationToken);
            //写入上一个node1的剩余子节点
            _stream.Seek(-nodeFileSize, SeekOrigin.Current);
            await WriteNode0ListAsync(_stream, nodeFileSize, outputStream, cancellationToken);
        }
        if (fileRmainSize == 0)
        {
            return cidData;
        }
        while (nodeFileSize < partSize)
        {
            var node1Size = Math.Min(fileRmainSize, node1MaxSize);
            nodeFileSize += node1Size;
            if (node1Size <= NODE0_MAX_SIZE)
            {
                var node0 = await ReadNode0Async(_stream, fileRmainSize, cancellationToken);
                await WritePBNodeAsync(outputStream, node0, cancellationToken);
                break;
            }
            var node1CutSize = node1Size;
            if (nodeFileSize > partSize)
            {
                node1CutSize -= nodeFileSize - partSize;
                nodeFileSize = partSize;
            }
            fileRmainSize -= node1CutSize;
            using (var memStream = new MemoryStream())
            {
                var node1 = await ReadNode1Async(_stream, node1Size, memStream, cancellationToken);
                await WritePBNodeAsync(outputStream, node1, cancellationToken);
                if (node1Size == node1CutSize)
                {
                    memStream.Seek(0, SeekOrigin.Begin);
                    await memStream.CopyToAsync(outputStream, cancellationToken);
                }
                else
                {
                    _stream.Seek(-node1Size, SeekOrigin.Current);
                    await WriteNode0ListAsync(_stream, node1CutSize, outputStream, cancellationToken);
                }
            }
            //
            if (fileRmainSize == 0)
            {
                break;
            }
        }
        return cidData;
    }
}