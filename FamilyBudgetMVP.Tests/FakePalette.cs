using SkiaSharp;

namespace FamilyBudgetMVP.Tests
{
    /// <summary>Подстава вместо CategoryStore: цвет по имени из словаря.</summary>
    public class FakePalette : FamilyBudgetMVP.Services.ICategoryPalette
    {
        public Dictionary<string, string> Colors { get; } = new()
        {
            ["Продукты"] = "#F59E0B",
            ["Транспорт"] = "#0EA5E9"
        };

        public SKColor GetChartColor(string? name) =>
            SKColor.Parse(name != null && Colors.TryGetValue(name, out var hex) ? hex : "#64748B");
    }
}
