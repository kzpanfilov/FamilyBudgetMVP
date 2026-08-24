using SkiaSharp;

namespace FamilyBudgetMVP.Services
{
    /// <summary>Источник цветов категорий для графика (реализуется хранилищем приложения).</summary>
    public interface ICategoryPalette
    {
        SKColor GetChartColor(string? name);
    }
}
