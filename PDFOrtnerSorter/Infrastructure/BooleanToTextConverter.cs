using System;
using System.Globalization;
using System.Windows.Data;

namespace PDFOrtnerSorter.Infrastructure;

public sealed class BooleanToTextConverter : IValueConverter
{
    public string TrueText { get; set; } = "Ja";
    public string FalseText { get; set; } = "Nein";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool boolean && boolean ? TrueText : FalseText;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
