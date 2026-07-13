using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using PCHub.Shared.Enums;

namespace PCHub.Client.Converters;

/// <summary>Konversi boolean ke Visibility (true = Visible, false = Collapsed)</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}

/// <summary>Konversi inverse boolean ke Visibility (false = Visible, true = Collapsed)</summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Collapsed;
}

/// <summary>Konversi boolean ke string teks</summary>
public class BoolToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            var options = parameter?.ToString()?.Split('|');
            if (options != null && options.Length == 2)
                return b ? options[0] : options[1];
            return b ? "Yes" : "No";
        }
        return "No";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Konversi PcStatus ke warna</summary>
public class PcStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PcStatus status)
        {
            return status switch
            {
                PcStatus.Available => new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)),  // Green
                PcStatus.InUse => new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)),      // Blue
                PcStatus.Maintenance => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)), // Yellow
                PcStatus.Broken => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),      // Red
                PcStatus.Reserved => new SolidColorBrush(Color.FromRgb(0x8B, 0x5C, 0xF6)),    // Purple
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Konversi BillingStatus ke warna</summary>
public class BillingStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is BillingStatus status)
        {
            return status switch
            {
                BillingStatus.Active => new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)),
                BillingStatus.Completed => new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)),
                BillingStatus.Paused => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
                BillingStatus.Locked => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Konversi decimal ke format rupiah</summary>
public class CurrencyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal d)
            return $"Rp {d:N0}";
        if (value is double db)
            return $"Rp {db:N0}";
        return "Rp 0";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Konversi TimeSpan ke format timer (HH:mm:ss)</summary>
public class TimeSpanToTimerConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TimeSpan ts)
            return ts.ToString(@"hh\:mm\:ss");
        return "00:00:00";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Konversi persentase ke warna (hijau rendah, kuning medium, merah tinggi)</summary>
public class PercentageToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double pct = 0;
        if (value is double d) pct = d;
        else if (value is float f) pct = f;
        else if (value is int i) pct = i;

        return pct switch
        {
            < 50 => new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)),
            < 80 => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
            _ => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
