using FamilyBudgetMVP.Models;
using Microsoft.Maui.Graphics;
using SQLite;
using System.IO;
using SkiaSharp;

namespace FamilyBudgetMVP.Services
{
    /// <summary>
    /// Хранилище категорий: таблица в той же БД budget.db.
    /// При первом запуске заполняется дефолтным набором. Держит кэш All и
    /// уведомляет подписчиков через Changed после каждой мутации.
    /// </summary>
    public class CategoryStore : ICategoryPalette
    {
        private readonly SQLiteAsyncConnection _database;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private bool _initialized;

        // Одна база на приложение — общая с TransactionService
        private static string DbPath => Path.Combine(FileSystem.AppDataDirectory, "budget.db");

        public List<Category> All { get; private set; } = new();

        /// <summary>Вызывается после любого изменения списка категорий.</summary>
        public event Action? Changed;

        public CategoryStore()
        {
            // Только лёгкие операции: вся работа с БД — в InitializeAsync().
            // Блокировать (.Wait/.Result) нельзя: вызов происходит на UI-потоке,
            // и await внутри методов встанет в очередь недоступного диспетчера.
            _database = new SQLiteAsyncConnection(DbPath);
        }

        /// <summary>
        /// Идемпотентная инициализация: миграции схемы, сид дефолтов, загрузка кэша.
        /// Вызывать перед первым использованием (страницы делают это в OnAppearing).
        /// </summary>
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
                await SeedIfEmptyAsync();
                await ReloadAsync();

                _initialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        private async Task SeedIfEmptyAsync()
        {
            if (await _database.Table<Category>().CountAsync() > 0)
                return;

            var defaults = new List<Category>();
            string[][] seed =
            [
                ["Продукты",    "🛒", "#F59E0B", "#FEF3C7"],
                ["Транспорт",   "🚌", "#0EA5E9", "#E0F2FE"],
                ["Жилье",       "🏠", "#8B5CF6", "#EDE9FE"],
                ["Развлечения", "🎬", "#EC4899", "#FCE7F3"],
                ["Здоровье",    "💊", "#22C55E", "#DCFCE7"],
                ["Разное",      "📦", "#64748B", "#E2E8F0"]
            ];

            for (int i = 0; i < seed.Length; i++)
            {
                defaults.Add(new Category
                {
                    Name = seed[i][0],
                    Icon = seed[i][1],
                    ColorHex = seed[i][2],
                    TintHex = seed[i][3],
                    SortOrder = i
                });
            }

            await _database.InsertAllAsync(defaults);
        }

        public async Task ReloadAsync()
        {
            All = await _database.Table<Category>().OrderBy(c => c.SortOrder).ToListAsync();
            Changed?.Invoke();
        }

        public async Task AddAsync(Category category)
        {
            category.SortOrder = All.Count;
            await _database.InsertAsync(category);
            await ReloadAsync();
        }

        public async Task UpdateAsync(Category category)
        {
            await _database.UpdateAsync(category);
            await ReloadAsync();
        }

        public async Task DeleteAsync(Category category)
        {
            await _database.DeleteAsync(category);
            await ReloadAsync();
        }

        // --- Поиск и цвета (с безопасным фолбэком для удалённых категорий) ---

        public Category? Find(string? name) =>
            All.FirstOrDefault(c => c.Name == name);

        public List<string> Names => All.Select(c => c.Name).ToList();

        public string GetIcon(string? name) => Find(name)?.Icon ?? "📦";

        public SKColor GetChartColor(string? name) =>
            SKColor.Parse(Find(name)?.ColorHex ?? "#64748B");

        public Color GetChartMauiColor(string? name) =>
            Color.FromArgb(Find(name)?.ColorHex ?? "#64748B");

        public Color GetTint(string? name) =>
            Color.FromArgb(Find(name)?.TintHex ?? "#E2E8F0");

        // Пастельный фон для новой категории: основной цвет, осветлённый до 85%
        public static string ComputeTint(string colorHex)
        {
            var c = Color.FromArgb(colorHex);
            const float mix = 0.85f;

            byte L(float channel) => (byte)Math.Round((channel + (1f - channel) * mix) * 255);

            return $"#{L(c.Red):X2}{L(c.Green):X2}{L(c.Blue):X2}";
        }
    }
}
