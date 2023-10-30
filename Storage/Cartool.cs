using System.Security.Cryptography;
using Google.Protobuf;
using Liuguang.Storage.Pb;

namespace Liuguang.Storage;

public static class CarTool
{
    public static (byte[], int) PackPBNode(PBNode pbNode)
    {
        var pbNodeRaw = pbNode.ToByteArray();
        var cidData = CalcCid(pbNodeRaw);
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

    public static async Task<(byte[], int)> WritePBNodeAsync(Stream outputStream, PBNode pbNode, CancellationToken cancellationToken)
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
    public static async Task WriteCarHeadAsync(Stream outputStream, byte[] cid, CancellationToken cancellationToken)
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
}