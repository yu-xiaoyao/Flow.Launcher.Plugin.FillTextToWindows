using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Flow.Launcher.Plugin.FillTextToWindows.Views
{
    /// <summary>
    /// true 就 <c>Collapsed</c>、false 才 <c>Visible</c>，正好和 WPF 自带的
    /// <c>BooleanToVisibilityConverter</c> 反过来。
    /// <para>
    /// 数据行上「不是最后一段才显示粘贴后按键」这种条件得反过来判，而内置的只有正向那个。
    /// </para>
    /// </summary>
    public sealed class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool flag && flag ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is not Visibility.Visible;
        }
    }
}
