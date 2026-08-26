using SQLite;

namespace FamilyBudgetMVP.Models
{
    /// <summary>Запись справочника льгот и субсидий.</summary>
    [Table("benefits")]
    public class Benefit
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // «Самарская область», «Саратовская область»
        public string Region { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        // Условия получения (кто имеет право)
        public string Conditions { get; set; } = string.Empty;

        // Перечень документов через «•»
        public string Documents { get; set; } = string.Empty;

        // Куда подавать: МФЦ / соцзащита / Госуслуги
        public string WhereToApply { get; set; } = string.Empty;

        // Теги для поиска: «многодетные, ипотека, ЖКХ»
        public string Tags { get; set; } = string.Empty;

        // Дата актуальности данных («актуально на …» по ТЗ)
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>Шаблон документа (статический каталог, контент — в Raw-ресурсах).</summary>
    public class DocTemplate
    {
        public required string Title { get; init; }
        public required string Description { get; init; }
        /// <summary>Путь к текстовому файлу внутри пакета (Resources/Raw).</summary>
        public required string AssetPath { get; init; }
    }
}
