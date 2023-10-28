using System.Security.Cryptography;
using Google.Protobuf;
using Liuguang.Storage.Pb;
using UnixFsData = Liuguang.Storage.Pb.Data;

namespace Liuguang.Storage;

public class CarContainer1
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

    /// <summary>
    /// 每次任务上传的文件内容大小
    /// 43.5MB *2 = 87MB
    /// </summary>
    const int TASK_LINK_MAX_COUNT = 2;

    public CarContainer1(Stream stream)
    {
        _stream = stream;
        _fileSize = stream.Length;
    }

    public static long FilePartMaxSize()
    {
        return NODE0_MAX_SIZE * NODE1_MAX_CHILDREN_COUNT * TASK_LINK_MAX_COUNT;
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
}