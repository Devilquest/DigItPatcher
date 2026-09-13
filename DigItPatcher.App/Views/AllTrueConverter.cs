using System.Globalization;
using System.Windows.Data;

namespace DigItPatcher.App.Views;

/// <summary>True when every bound value is true, which is how a control answers to more than one condition.</summary>
internal sealed class AllTrueConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        => values.All(value => value is true);

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
