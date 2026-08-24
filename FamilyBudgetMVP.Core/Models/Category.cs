using SQLite;

namespace FamilyBudgetMVP.Models
{
    /// <summary>Категория операций. Хранится в БД, редактируется в настройках.</summary>
    [Table("categories")]
    public class Category
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        // Эмодзи-иконка аватара
        public string Icon { get; set; } = "📦";

        // Основной цвет (столбец графика, точка в списке)
        public string ColorHex { get; set; } = "#64748B";

        // Пастельный фон аватара
        public string TintHex { get; set; } = "#E2E8F0";

        // Месячный лимит расходов, 0 — без лимита
        public decimal MonthlyLimit { get; set; }

        public int SortOrder { get; set; }

        // Подпись лимита для списка настроек
        public string LimitText => MonthlyLimit > 0 ? $"лимит {MonthlyLimit:N0} ₽/мес" : "без лимита";
    }
}
