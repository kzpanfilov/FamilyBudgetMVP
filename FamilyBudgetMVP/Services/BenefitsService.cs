using System.IO;
using System.Text.Json;
using FamilyBudgetMVP.Models;
using SQLite;

namespace FamilyBudgetMVP.Services
{
    /// <summary>
    /// Справочник льгот: импорт из JSON в SQLite при первом запуске,
    /// поиск по региону и ключевым словам. Обновление — заменой JSON-ресурса.
    /// </summary>
    public class BenefitsService
    {
        private const string SeedAsset = "benefits.json";

        /// <summary>URL JSON-справочника по умолчанию. Заменить на реальный адрес перед публикацией.</summary>
        public const string DefaultCatalogUrl = "https://raw.githubusercontent.com/familybudget/catalog/main/benefits.json";

        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

        private readonly SQLiteAsyncConnection _database;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private bool _initialized;

        private static string DbPath => Path.Combine(FileSystem.AppDataDirectory, "budget.db");

        public BenefitsService()
        {
            _database = new SQLiteAsyncConnection(DbPath);
        }

        public async Task InitializeAsync()
        {
            if (_initialized)
                return;

            await _initLock.WaitAsync();
            try
            {
                if (_initialized)
                    return;

                await DbMigrations.ApplyAsync(_database);

                // Импорт справочника при первом запуске (таблица пуста)
                int count = await _database.Table<Benefit>().CountAsync();
                if (count == 0)
                    await ImportFromAssetAsync(SeedAsset);

                _initialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        /// <summary>Импорт записей из JSON-ресурса пакета (механизм обновления справочника).</summary>
        public async Task ImportFromAssetAsync(string assetPath)
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync(assetPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var items = await JsonSerializer.DeserializeAsync<List<Benefit>>(stream, options);

            if (items == null || items.Count == 0)
                return;

            foreach (var item in items)
                item.Id = 0; // автоприращение ключа

            await _database.InsertAllAsync(items);
        }

        /// <summary>
        /// Скачивает JSON-справочник по URL, заменяет содержимое таблицы benefits
        /// и возвращает количество загруженных записей. Бросает исключение при ошибке сети.
        /// </summary>
        public async Task<int> RefreshFromUrlAsync(string url)
        {
            await InitializeAsync();

            string json = await _http.GetStringAsync(url);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var items = JsonSerializer.Deserialize<List<Benefit>>(json, options);

            if (items == null || items.Count == 0)
                throw new InvalidOperationException("Справочник пуст или имеет неверный формат");

            foreach (var item in items)
            {
                item.Id = 0;
                if (item.UpdatedAt == default)
                    item.UpdatedAt = DateTime.Today;
            }

            await _database.RunInTransactionAsync(tr =>
            {
                tr.Execute("DELETE FROM benefits");
                tr.InsertAll(items);
            });

            return items.Count;
        }

        /// <summary>Список регионов справочника, отсортированный по алфавиту.</summary>
        public async Task<List<string>> GetRegionsAsync()
        {
            await InitializeAsync();
            var all = await _database.Table<Benefit>().ToListAsync();
            return all.Select(b => b.Region).Distinct().OrderBy(r => r).ToList();
        }

        /// <summary>
        /// Поиск: регион (null/пусто — все) и строка запроса по названию,
        /// описанию, условиям и тегам.
        /// </summary>
        public async Task<List<Benefit>> SearchAsync(string? region, string? query)
        {
            await InitializeAsync();

            IEnumerable<Benefit> items = await _database.Table<Benefit>().ToListAsync();

            if (!string.IsNullOrWhiteSpace(region))
                items = items.Where(b => b.Region == region);

            string q = (query ?? string.Empty).Trim();
            if (q.Length > 0)
                items = items.Where(b =>
                    b.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    b.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    b.Conditions.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    b.Tags.Contains(q, StringComparison.OrdinalIgnoreCase));

            return items.OrderBy(b => b.Region).ThenBy(b => b.Name).ToList();
        }

        public async Task<DateTime> GetLatestUpdatedAtAsync()
        {
            await InitializeAsync();
            var all = await _database.Table<Benefit>().ToListAsync();
            return all.Count == 0 ? DateTime.Today : all.Max(b => b.UpdatedAt);
        }
    }

    /// <summary>Статический каталог шаблонов документов (ТЗ MVP, этап 3).</summary>
    public static class DocTemplateCatalog
    {
        public static readonly IReadOnlyList<DocTemplate> All = new List<DocTemplate>
        {
            new()
            {
                Title = "Заявление на субсидию ЖКУ",
                Description = "Для оформления субсидии на оплату жилищно-коммунальных услуг",
                AssetPath = "templates/subsidiya-zhku.txt"
            },
            new()
            {
                Title = "Справка о составе семьи",
                Description = "Что это за документ и где его взять",
                AssetPath = "templates/spravka-sostav-semi.txt"
            },
            new()
            {
                Title = "Заявление на единое пособие",
                Description = "Пособие в связи с рождением и воспитанием ребёнка",
                AssetPath = "templates/edinaya-posobie.txt"
            }
        };

        /// <summary>Читает текст шаблона из ресурсов пакета.</summary>
        public static async Task<string> LoadContentAsync(DocTemplate template)
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync(template.AssetPath);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
    }
}
