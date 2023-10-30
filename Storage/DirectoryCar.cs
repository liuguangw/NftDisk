using Google.Protobuf;
using Liuguang.Storage.Pb;
using UnixFsData = Liuguang.Storage.Pb.Data;

namespace Liuguang.Storage;

public class DirectoryCar
{
    public class ChildItem
    {
        private byte[] _cidData;
        public byte[] CidData
        {
            get => _cidData;
            set { _cidData = value; }
        }
        public string Name { get; set; } = string.Empty;
        public long TSize { get; set; } = 0;
        public ChildItem(byte[] cidData)
        {
            _cidData = cidData;
        }
    }
    public readonly List<ChildItem> Items = new();

    private PBNode BuildRootNode()
    {
        var rootNode = new PBNode();
        var unixfsData = new byte[] { 0x08, 0x01 };
        rootNode.Data = ByteString.CopyFrom(unixfsData);
        foreach (var item in Items)
        {
            rootNode.Links.Add(new PBLink()
            {
                Hash = ByteString.CopyFrom(item.CidData),
                Name = item.Name,
                Tsize = (ulong)item.TSize,
            });
        }
        return rootNode;
    }
    private static PBNode BuildPadNode()
    {
        var commonPbNode = new PBNode();
        var unixfsData = new UnixFsData()
        {
            Type = UnixFsData.Types.DataType.File,
            Data_ = ByteString.Empty,
            Filesize = 0
        };
        commonPbNode.Data = unixfsData.ToByteString();
        return commonPbNode;
    }

    public byte[] GetCID()
    {
        var rootPbNode = BuildRootNode();
        var (cidData, _) = CarTool.PackPBNode(rootPbNode);
        return cidData;
    }

    public async Task<byte[]> WriteCarAsync(Stream outputStream, CancellationToken cancellationToken = default)
    {
        var rootPbNode = BuildRootNode();
        var (cidData, _) = CarTool.PackPBNode(rootPbNode);
        await CarTool.WriteCarHeadAsync(outputStream, cidData, cancellationToken);
        await CarTool.WritePBNodeAsync(outputStream, rootPbNode, cancellationToken);
        var padNode = BuildPadNode();
        await CarTool.WritePBNodeAsync(outputStream, padNode, cancellationToken);
        return cidData;
    }
}