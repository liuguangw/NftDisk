namespace Liuguang.NftDisk.Common;
public static class SizeTool
{
    public static string ParseSize(long sizeValue)
    {
        const long KB = 1024;
        const long MB = 1024 * KB;
        const long GB = 1024 * MB;
        if (sizeValue < 0)
        {
            //出错了
            return "?";
        }
        else if (sizeValue >= 0 && sizeValue < KB)
        {
            return $"{sizeValue} B";
        }
        else if (sizeValue >= KB && sizeValue < MB)
        {
            double v = (double)sizeValue / KB;
            return $"{v:N2} KB";
        }
        else if (sizeValue >= MB && sizeValue < GB)
        {
            double v = (double)sizeValue / MB;
            return $"{v:N2} MB";
        }
        else
        {
            double v = (double)sizeValue / GB;
            return $"{v:N2} GB";
        }
    }
}