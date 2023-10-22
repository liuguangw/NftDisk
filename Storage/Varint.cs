namespace Liuguang.Storage;
public static class Varint
{
    public static byte[] ToUvarint(ulong x)
    {
        var buf = new byte[10];
        int i = 0;
        while (x >= 0x80)
        {
            buf[i] = (byte)((int)x | 0x80);
            x >>= 7;
            i++;
        }
        buf[i] = (byte)x;
        return buf[..(i + 1)];
    }
}
