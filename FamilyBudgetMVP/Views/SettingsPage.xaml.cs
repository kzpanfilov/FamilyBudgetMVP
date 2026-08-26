using System.Globalization;
using FamilyBudgetMVP.Helpers;
using FamilyBudgetMVP.Models;
using FamilyBudgetMVP.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace FamilyBudgetMVP.Views
{
    /// <summary>Настройки: категории (CRUD, лимиты) и экспорт данных.</summary>
    public partial class SettingsPage : ContentPage
    {
        private readonly CategoryStore _categories = ServiceHelper.Get<CategoryStore>();
        private readonly TransactionService _txService = ServiceHelper.Get<TransactionService>();

        // Палитра для новых категорий — фирменные цвета приложения
        private static readonly string[] Palette =
        [
            "#F59E0B", "#0EA5E9", "#8B5CF6", "#EC4899", "#22C55E", "#64748B",
            "#EF4444", "#14B8A6"
        ];

        private static readonly string[] PaletteNames =
        [
            "Янтарный", "Небесный", "Лавандовый", "Розовый",
            "Изумрудный", "Серый", "Красный", "Бирюзовый"
        ];

        private string _selectedColorHex = "#0EA5E9";
        private readonly List<Border> _swatches = new();

        public SettingsPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                // Кэш категорий должен быть готов до показа списка и палитры
                await _categories.InitializeAsync();
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "Настройки: инициализация");
            }

            CategoriesList.ItemsSource = _categories.All;
            BuildSwatches();
        }

        // --- Палитра выбора цвета ---

        private void BuildSwatches()
        {
            SwatchPanel.Children.Clear();
            _swatches.Clear();

            foreach (var hex in Palette)
            {
                var swatch = new Border
                {
                    WidthRequest = 30,
                    HeightRequest = 30,
                    StrokeThickness = 2,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(),
                    BackgroundColor = Color.FromArgb(hex),
                    BindingContext = hex
                };

                swatch.GestureRecognizers.Add(new TapGestureRecognizer
                {
                    Command = new Command<string>(OnSwatchTapped),
                    CommandParameter = hex
                });

                _swatches.Add(swatch);
                SwatchPanel.Children.Add(swatch);
            }

            HighlightSwatch(_selectedColorHex);
        }

        private void OnSwatchTapped(string hex)
        {
            _selectedColorHex = hex;
            HighlightSwatch(hex);
        }

        private void HighlightSwatch(string hex)
        {
            var ringColor = Color.FromArgb("#5B6B70"); // виден и на светлой, и на тёмной теме

            foreach (var swatch in _swatches)
            {
                bool selected = (string?)swatch.BindingContext == hex;
                swatch.Stroke = new SolidColorBrush(ringColor);
                swatch.StrokeThickness = selected ? 2 : 0;
                swatch.Scale = selected ? 1.15 : 1.0;
            }
        }

        // --- Редактирование существующей категории (клик по строке) ---

        private async void OnCategoryTapped(object? sender, TappedEventArgs e)
        {
            if ((sender as BindableObject)?.BindingContext is not Category category)
                return;

            string? action = await DisplayActionSheetAsync(category.Name, "Отмена", null,
                "Месячный лимит", "Изменить цвет", "Изменить иконку");

            try
            {
                switch (action)
                {
                    case "Месячный лимит":
                        await EditLimitAsync(category);
                        break;
                    case "Изменить цвет":
                        await EditColorAsync(category);
                        break;
                    case "Изменить иконку":
                        await EditIconAsync(category);
                        break;
                }
            }
            catch (Exception ex)
            {
                LogService.Error(ex, $"Категория «{category.Name}»: правка");
                await DisplayAlertAsync("Ошибка БД", $"Не удалось сохранить изменения: {ex.Message}", "OK");
            }
        }

        private async Task EditLimitAsync(Category category)
        {
            string? input = await DisplayPromptAsync(
                "Месячный лимит",
                $"Категория «{category.Name}».\nВведите сумму расходов в месяц. 0 — без лимита.",
                initialValue: category.MonthlyLimit > 0 ? category.MonthlyLimit.ToString("N0") : "0",
                keyboard: Keyboard.Numeric);

            if (input == null)
                return;

            // Принимаем и запятую, и точку
            if (!decimal.TryParse(input.Trim().Replace('.', ','), NumberStyles.Number,
                    CultureInfo.GetCultureInfo("ru-RU"), out decimal limit) || limit < 0)
            {
                await DisplayAlertAsync("Ошибка", "Введите корректную сумму.", "OK");
                return;
            }

            category.MonthlyLimit = limit;
            await _categories.UpdateAsync(category);
            CategoriesList.ItemsSource = _categories.All;
        }

        private async Task EditColorAsync(Category category)
        {
            string? picked = await DisplayActionSheetAsync(
                $"Цвет «{category.Name}»", "Отмена", null, PaletteNames);

            if (picked == null || Array.IndexOf(PaletteNames, picked) < 0)
                return;

            int index = Array.IndexOf(PaletteNames, picked);
            category.ColorHex = Palette[index];
            category.TintHex = CategoryStore.ComputeTint(Palette[index]);

            await _categories.UpdateAsync(category);
            CategoriesList.ItemsSource = _categories.All;
        }

        private async Task EditIconAsync(Category category)
        {
            string? icon = await DisplayPromptAsync(
                "Иконка",
                "Введите эмодзи для аватара категории:",
                initialValue: category.Icon,
                maxLength: 4);

            if (string.IsNullOrWhiteSpace(icon))
                return;

            category.Icon = icon.Trim();
            await _categories.UpdateAsync(category);
            CategoriesList.ItemsSource = _categories.All;
        }

        // --- Экспорт CSV ---

        private async void OnExportClicked(object? sender, EventArgs e)
        {
            try
            {
                var transactions = await _txService.GetTransactionsAsync();

                if (transactions.Count == 0)
                {
                    await DisplayAlertAsync("Нет данных", "Пока нечего выгружать — операций нет.", "OK");
                    return;
                }

                string csv = CsvBuilder.BuildTransactionsCsv(transactions);
                string suggestedName = $"budget_{DateTime.Now:yyyy-MM-dd}.csv";

                string? path = await ServiceHelper.Get<IFileSaver>().SaveTextAsync(suggestedName, csv);

                if (path == null)
                {
                    await DisplayAlertAsync("Отменено", "Экспорт отменён.", "OK");
                    return;
                }

                bool open = await DisplayAlertAsync("Готово", $"Файл сохранён:\n{path}\n\nОткрыть его?", "Открыть", "Закрыть");
                if (open)
                    await Launcher.Default.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(path) });
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "Экспорт CSV");
                await DisplayAlertAsync("Ошибка экспорта", ex.Message, "OK");
            }
        }

        // --- Добавление и удаление ---

        private async void OnAddCategoryClicked(object? sender, EventArgs e)
        {
            string name = NameEntry.Text?.Trim() ?? string.Empty;

            if (name.Length == 0)
            {
                await DisplayAlertAsync("Ошибка", "Введите название категории!", "OK");
                return;
            }

            if (_categories.Find(name) != null)
            {
                await DisplayAlertAsync("Ошибка", "Категория с таким названием уже есть.", "OK");
                return;
            }

            try
            {
                await _categories.AddAsync(new Category
                {
                    Name = name,
                    Icon = string.IsNullOrWhiteSpace(IconEntry.Text) ? "📦" : IconEntry.Text.Trim(),
                    ColorHex = _selectedColorHex,
                    TintHex = CategoryStore.ComputeTint(_selectedColorHex)
                });

                LogService.Info($"Добавлена категория «{name}»");

                NameEntry.Text = string.Empty;
                IconEntry.Text = string.Empty;

                CategoriesList.ItemsSource = _categories.All;
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "Добавление категории");
                await DisplayAlertAsync("Ошибка БД", $"Не удалось добавить категорию: {ex.Message}", "OK");
            }
        }

        private async void OnDeleteCategoryClicked(object? sender, EventArgs e)
        {
            if ((sender as BindableObject)?.BindingContext is not Category category)
                return;

            // Нельзя удалять категорию, которая используется в операциях
            var transactions = await _txService.GetTransactionsAsync();
            int usedCount = transactions.Count(t => t.Category == category.Name);

            if (usedCount > 0)
            {
                await DisplayAlertAsync("Нельзя удалить",
                    $"Категория «{category.Name}» используется в {usedCount} операциях.",
                    "OK");
                return;
            }

            bool confirm = await DisplayAlertAsync("Подтверждение",
                $"Удалить категорию «{category.Name}»?", "Да", "Нет");
            if (!confirm) return;

            try
            {
                await _categories.DeleteAsync(category);
                CategoriesList.ItemsSource = _categories.All;
            }
            catch (Exception ex)
            {
                LogService.Error(ex, $"Удаление категории «{category.Name}»");
                await DisplayAlertAsync("Ошибка БД", $"Не удалось удалить: {ex.Message}", "OK");
            }
        }
    }
}
