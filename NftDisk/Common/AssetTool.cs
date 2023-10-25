using System;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Liuguang.NftDisk.Common;

public static class AssetTool
{
    const string ICON_PATH_PREFIX = "avares://NftDisk/Assets/icons";
    public static Bitmap LoadImage(string path)
    {
        return new Bitmap(AssetLoader.Open(new Uri(path)));
    }

    public static Bitmap LoadIconImage(string path)
    {
        return LoadImage($"{ICON_PATH_PREFIX}/{path}");
    }
}