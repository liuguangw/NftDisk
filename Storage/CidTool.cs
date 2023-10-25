using SimpleBase;

namespace Liuguang.Storage;

public static class CidTool
{
    public static string ToV0String(byte[] cidData)
    {
        return Base58.Bitcoin.Encode(cidData);
    }

    public static string ToV1String(byte[] cidData)
    {
        var base32CidData = new byte[2 + cidData.Length];
        base32CidData[0] = 0x1;//version cid v1
        base32CidData[1] = 0x70;//dag-pb
        cidData.CopyTo(base32CidData, 2);
        return "b" + Base32.FileCoin.Encode(base32CidData);
    }
}