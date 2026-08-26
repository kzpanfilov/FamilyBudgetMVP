using System.Globalization;
using FamilyBudgetMVP.Helpers;
using FamilyBudgetMVP.Services;
using Microsoft.Maui.Controls;

namespace FamilyBudgetMVP.Converters
{
    public class CategoryIconConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => ServiceHelper.Get<CategoryStore>().GetIcon(value as string);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class CategoryTintConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => ServiceHelper.Get<CategoryStore>().GetTint(value as string);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // Основной цвет категории (не пастельный) — по имени категории
    public class CategoryColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => ServiceHelper.Get<CategoryStore>().GetChartMauiColor(value as string);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class HexToColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => Microsoft.Maui.Graphics.Color.FromArgb(value as string ?? "#64748B");

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
