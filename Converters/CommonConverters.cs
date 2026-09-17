using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Wpf.Ui.Controls;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace KuaiKuaiLaunch.Converters
{
    /// <summary>
    /// 布尔值与 UI 可见性 (Visibility) 转换器
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public static readonly BooleanToVisibilityConverter Instance = new();

        /// <summary>
        /// 将布尔值转换为 Visibility，支持 "Invert" 反向转换
        /// </summary>
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isTrue = value is bool b && b;
            if (parameter is string paramStr && paramStr.Equals("Invert", StringComparison.OrdinalIgnoreCase))
            {
                isTrue = !isTrue;
            }
            return isTrue ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 将 Visibility 反向转换为布尔值
        /// </summary>
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is Visibility v && v == Visibility.Visible;
        }
    }

    /// <summary>
    /// 字符串非空与 UI 可见性 (Visibility) 转换器
    /// </summary>
    public class StringNullOrEmptyToVisibilityConverter : IValueConverter
    {
        public static readonly StringNullOrEmptyToVisibilityConverter Instance = new();

        /// <summary>
        /// 当字符串非空时返回 Visible，为空或全空白时返回 Collapsed
        /// </summary>
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isEmpty = string.IsNullOrWhiteSpace(value as string);
            return isEmpty ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>
        /// 不支持反向转换
        /// </summary>
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 数量计数与 UI 可见性 (Visibility) 转换器
    /// </summary>
    public class CountToVisibilityConverter : IValueConverter
    {
        public static readonly CountToVisibilityConverter Instance = new();

        /// <summary>
        /// 数量转换为 Visibility。参数为 "Empty" 时为 0 显示，否则大于 0 显示
        /// </summary>
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            int count = value is int c ? c : 0;
            bool showIfEmpty = parameter is string s && s.Equals("Empty", StringComparison.OrdinalIgnoreCase);
            if (showIfEmpty)
            {
                return count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            return count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 不支持反向转换
        /// </summary>
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 布尔值与图钉图标 (SymbolRegular) 转换器
    /// </summary>
    public class BooleanToPinSymbolConverter : IValueConverter
    {
        public static readonly BooleanToPinSymbolConverter Instance = new();

        /// <summary>
        /// 根据是否置顶返回对应图钉图标
        /// </summary>
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isPinned = value is bool b && b;
            return isPinned ? SymbolRegular.Pin24 : SymbolRegular.PinOff24;
        }

        /// <summary>
        /// 不支持反向转换
        /// </summary>
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 布尔值与图钉悬浮提示文字转换器
    /// </summary>
    public class BooleanToPinTooltipConverter : IValueConverter
    {
        public static readonly BooleanToPinTooltipConverter Instance = new();

        /// <summary>
        /// 根据置顶状态返回悬浮提示文本
        /// </summary>
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isTop = value is bool b && b;
            return isTop ? "取消置顶 (当前已置顶)" : "窗口置顶";
        }

        /// <summary>
        /// 不支持反向转换
        /// </summary>
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 布尔值与图钉图标画刷高亮颜色转换器
    /// </summary>
    public class BooleanToPinBrushConverter : IValueConverter
    {
        public static readonly BooleanToPinBrushConverter Instance = new();

        /// <summary>
        /// 置顶时返回主题高亮色或 DodgerBlue，未置顶时返回次级文本色或 Gray
        /// </summary>
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isTop = value is bool b && b;
            if (isTop)
            {
                if (Application.Current?.TryFindResource("AccentTextFillColorPrimaryBrush") is Brush brush)
                {
                    return brush;
                }
                return Brushes.DodgerBlue;
            }
            if (Application.Current?.TryFindResource("TextFillColorSecondaryBrush") is Brush secBrush)
            {
                return secBrush;
            }
            return Brushes.Gray;
        }

        /// <summary>
        /// 不支持反向转换
        /// </summary>
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 布尔值与展开折叠箭头图标转换器
    /// </summary>
    public class BooleanToChevronSymbolConverter : IValueConverter
    {
        public static readonly BooleanToChevronSymbolConverter Instance = new();

        /// <summary>
        /// 展开返回 ChevronDown24，折叠返回 ChevronRight24
        /// </summary>
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isExpanded = value is bool b && b;
            return isExpanded ? SymbolRegular.ChevronDown24 : SymbolRegular.ChevronRight24;
        }

        /// <summary>
        /// 不支持反向转换
        /// </summary>
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 图标名称字符串与 WPF-UI SymbolRegular 枚举转换器
    /// </summary>
    public class SymbolNameToSymbolRegularConverter : IValueConverter
    {
        public static readonly SymbolNameToSymbolRegularConverter Instance = new();

        /// <summary>
        /// 将字符串解析为 SymbolRegular 枚举，若解析失败返回 Apps24
        /// </summary>
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string symbolName && Enum.TryParse(typeof(SymbolRegular), symbolName, true, out var symbol))
            {
                return symbol;
            }
            return SymbolRegular.Apps24;
        }

        /// <summary>
        /// 将 SymbolRegular 枚举转为字符串
        /// </summary>
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value?.ToString() ?? "Apps24";
        }
    }
}

