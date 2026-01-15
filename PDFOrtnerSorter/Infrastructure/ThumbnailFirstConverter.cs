using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace PDFOrtnerSorter.Infrastructure;

public class ThumbnailFirstConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ReadOnlyObservableCollection<BitmapImage> collection && collection.Count > 0)
        {
            return collection[0];
        }

        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
