using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Nitrox.Launcher.Models.Converters;

/// <summary>
/// Converts a string path to a bitmap asset.
/// The asset must be in the same assembly as the program. If it isn't,
/// specify "avares://<assemblynamehere>/" in front of the path to the asset.
/// </summary>
public class BitmapAssetValueConverter : BaseConverter<BitmapAssetValueConverter>, IValueConverter
{
    private static readonly string assemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? throw new Exception("Unable to get Assembly name");
    private static readonly Dictionary<string, Bitmap> assetCache = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
        {
            return null;
        }

        if (value is not string rawUri || !targetType.IsAssignableFrom(typeof(Bitmap)))
        {
            throw new NotSupportedException();
        }

        if (assetCache.TryGetValue(rawUri, out Bitmap bitmap))
        {
            return bitmap;
        }

        Uri uri;
        // Allow for assembly overrides
        if (rawUri.StartsWith("avares://"))
        {
            uri = new Uri(rawUri);
        }
        else
        {
            uri = new Uri($"avares://{assemblyName}{rawUri}");
        }

        IAssetLoader assets = AvaloniaLocator.Current.GetRequiredService<IAssetLoader>();
        Stream asset = assets.Open(uri);

        bitmap = new Bitmap(asset);
        assetCache.Add(rawUri, bitmap);

        return bitmap;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
