using ADLib.Logging;
using ADLib.Util;
using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace WPFUtil;

public static class ImageHelper
{
    public static BitmapSource? GetImage(string? file, Action<BitmapImage>? setBitmapOptions = null)
    {
        if (file.IsEmpty() || !File.Exists(file))
            return null;

        try
        {
            return GetDPIFixedBitmap(file, setBitmapOptions);
        }
        catch (Exception e)
        {
            GenLog.Error(e.Message);
            GenLog.Error($"Error loading image '{file}'. Will retry without colour profile.");
        }

        var bitmapOptionsSetter = new Action<BitmapImage>(b =>
        {
            setBitmapOptions?.Invoke(b);
            b.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        });

        return GetDPIFixedBitmap(file, bitmapOptionsSetter);
    }

    private static BitmapSource GetDPIFixedBitmap(string file, Action<BitmapImage>? setBitmapOptions)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        setBitmapOptions?.Invoke(bitmap);
        bitmap.UriSource = new Uri(file);
        bitmap.EndInit();
        var dpiFixedBitmap = ConvertBitmapToStandardDPI(bitmap);
        dpiFixedBitmap.Freeze();
        return dpiFixedBitmap;
    }

    private static BitmapSource ConvertBitmapToStandardDPI(BitmapSource bitmapImage)
    {
        const double dpi = 96;
        var width = bitmapImage.PixelWidth;
        var height = bitmapImage.PixelHeight;

        var stride = width * bitmapImage.Format.BitsPerPixel;
        var pixelData = new byte[stride * height];
        bitmapImage.CopyPixels(pixelData, stride, 0);

        return BitmapSource.Create(width, height, dpi, dpi, bitmapImage.Format, null, pixelData, stride);
    }
}