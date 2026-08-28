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

            var lockService = ServiceHelper.Get<LockService>();
            LockSwitch.IsToggled = lockService.IsEnabled;
            ChangePinButton.IsVisible = lockService.IsEnabled;

            RefreshPremiumUi();
            RefreshBudgetPeriodUi();
        }

        // --- Период бюджета ---

        private void RefreshBudgetPeriodUi()
        {
            var store = ServiceHelper.Get<BudgetPeriodStore>();
            var period = store.GetCurrent();

            // Пикеры показывают реальные даты текущего периода бюджета
            var (start, endExclusive) = period.Resolve(DateTime.Today);
            PeriodStartPicker.Date = start;
            PeriodEndPicker.Date = endExclusive.AddDays(-1);

            var isCalendarMonth = period == BudgetPeriod.CalendarMonth;
            BudgetPeriodHintLabel.Text = isCalendarMonth
                ? "Баланс и прогноз считаются за текущий календарный месяц."
                : $"Баланс и прогноз считаются за период: {period.FormatRange(DateTime.Today)}.\nПериод повторяется каждый месяц. Задайте дату начала и последний день.";
        }

        private void OnSavePeriodClicked(object? sender, EventArgs e)
        {
            var start = ((DateTime?)PeriodStartPicker.Date)?.Date ?? DateTime.Today;
            var end = ((DateTime?)PeriodEndPicker.Date)?.Date ?? DateTime.Today;

            if (end <= start)
            {
                DisplayAlertAsync("Ошибка", "Дата конца периода должна быть позже даты начала.", "OK");
                return;
            }

            // Период цикличен: запоминаем день месяца, диапазон повторяется каждый месяц
            ServiceHelper.Get<BudgetPeriodStore>().Save(new Models.BudgetPeriod(start.Day, end.Day));
            RefreshBudgetPeriodUi();

            var current = ServiceHelper.Get<BudgetPeriodStore>().GetCurrent();
            DisplayAlertAsync("Сохранено", $"Период бюджета: {current.FormatRange(DateTime.Today)}.\nОн повторяется каждый месяц. Дашборд пересчитает баланс и прогноз.", "OK");
        }

        private void OnResetPeriodClicked(object? sender, EventArgs e)
        {
            ServiceHelper.Get<BudgetPeriodStore>().ResetToCalendarMonth();
            RefreshBudgetPeriodUi();
            DisplayAlertAsync("Сброшено", "Период снова считается за календарный месяц.", "OK");
        }

        // --- Премиум: статус и активация по коду ---

        private void RefreshPremiumUi()
        {
            if (FeatureGate.IsPremium)
            {
                var until = FeatureGate.ValidUntilUtc ?? DateTime.UtcNow;
                PremiumStatusLabel.Text = $"⭐ Премиум активен{(FeatureGate.ValidUntilUtc != null ? $" до {until.ToLocalTime():d MMMM yyyy}" : " бессрочно")}";
                PremiumStatusLabel.FontFamily = "OpenSansSemibold";
                PremiumStatusLabel.TextColor = Color.FromArgb("#14B8A6");
                PremiumHintLabel.Text = "Все функции доступны. Спасибо за поддержку! ❤️";
                PremiumCodeEntry.IsVisible = false;
                PremiumCodeEntry.Text = string.Empty;
                ActivatePremiumButton.IsVisible = false;
            }
            else
            {
                PremiumStatusLabel.Text = "Бесплатная версия — Сценарии и полный справочник льгот доступны в премиуме";
                PremiumStatusLabel.FontFamily = "OpenSansRegular";
                PremiumStatusLabel.TextColor = Color.FromArgb("#94A3B8");
                PremiumHintLabel.Text = "Введи код активации (получают участники семейного проекта).";
                PremiumCodeEntry.IsVisible = true;
                ActivatePremiumButton.IsVisible = true;
            }
        }

        private async void OnActivatePremiumClicked(object? sender, EventArgs e)
        {
            var code = PremiumCodeEntry.Text?.Trim() ?? string.Empty;

            if (code.Length == 0)
            {
                await DisplayAlertAsync("Код активации", "Введи код, полученный от бота Бюджет+.", "OK");
                return;
            }

            var result = PremiumActivation.Validate(code, DateTime.UtcNow);

            switch (result.Status)
            {
                case PremiumActivationStatus.Valid when result.ValidUntilUtc is { } until:
                    try
                    {
                        var store = ServiceHelper.Get<IPremiumStore>();
                        store.Activate(until);

                        LogService.Info($"Премиум активирован до {until:yyyy-MM-dd}");
                        RefreshPremiumUi();
                        await DisplayAlertAsync("Премиум активирован 🎉",
                            $"Все функции доступны до {until.ToLocalTime():d MMMM yyyy}.", "Отлично");
                    }
                    catch (Exception ex)
                    {
                        LogService.Error(ex, "Активация премиума");
                        await DisplayAlertAsync("Ошибка", $"Не удалось сохранить активацию: {ex.Message}", "OK");
                    }
                    break;

                case PremiumActivationStatus.Expired:
                    await DisplayAlertAsync("Код истёк", "Срок действия этого кода уже закончился. Запроси новый код.", "OK");
                    break;

                default:
                    await DisplayAlertAsync("Неверный код", "Проверь код — он введён с ошибкой или не существует.", "OK");
                    break;
            }
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
                    BindingContext = hex,
                    Margin = new Thickness(0, 0, 8, 8)
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

        // --- Бэкап и восстановление БД ---

        private static string DbPath => Path.Combine(FileSystem.AppDataDirectory, "budget.db");

        private async void OnBackupClicked(object? sender, EventArgs e)
        {
            try
            {
                if (!File.Exists(DbPath))
                {
                    await DisplayAlertAsync("Ошибка", "База данных не найдена.", "OK");
                    return;
                }

                var date = DateTime.Now.ToString("yyyy-MM-dd_HHmm");
                var backupName = $"budget_backup_{date}.db";

                using var stream = File.OpenRead(DbPath);
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.Android, new[] { "application/octet-stream" } },
                        { DevicePlatform.iOS, new[] { "public.database" } },
                        { DevicePlatform.macOS, new[] { "public.database" } },
                        { DevicePlatform.WinUI, new[] { ".db" } }
                    }),
                    PickerTitle = "Сохранить бэкап"
                });

                if (result == null)
                    return;

                // Копируем БД в выбранный путь
                using var destStream = File.OpenWrite(result.FullPath);
                stream.Position = 0;
                await stream.CopyToAsync(destStream);

                await DisplayAlertAsync("Готово", $"Бэкап сохранён:\n{result.FullPath}", "OK");
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "Бэкап БД");
                await DisplayAlertAsync("Ошибка", ex.Message, "OK");
            }
        }

        private async void OnRestoreClicked(object? sender, EventArgs e)
        {
            try
            {
                bool confirm = await DisplayAlertAsync("Внимание",
                    "Восстановление заменит текущую базу данных. Все несохранённые данные будут потеряны.\n\nПродолжить?",
                    "Да", "Отмена");
                if (!confirm) return;

                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.Android, new[] { "application/octet-stream", ".db" } },
                        { DevicePlatform.iOS, new[] { "public.database" } },
                        { DevicePlatform.macOS, new[] { "public.database" } },
                        { DevicePlatform.WinUI, new[] { ".db" } }
                    }),
                    PickerTitle = "Выберите файл бэкапа"
                });

                if (result == null)
                    return;

                // Копируем в локальную папку, затем перезапускаем
                var tempPath = DbPath + ".restore";
                using (var sourceStream = File.OpenRead(result.FullPath))
                using (var destStream = File.OpenWrite(tempPath))
                {
                    await sourceStream.CopyToAsync(destStream);
                }

                // Заменяем текущую БД
                File.Copy(tempPath, DbPath, overwrite: true);
                File.Delete(tempPath);

                // Перезагружаем кэш категорий
                await _categories.ReloadAsync();

                await DisplayAlertAsync("Готово", "База данных восстановлена. Перезапустите приложение.", "OK");
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "Восстановление БД");
                await DisplayAlertAsync("Ошибка", ex.Message, "OK");
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

        private async void OnLockToggled(object? sender, ToggledEventArgs e)
        {
            var lockService = ServiceHelper.Get<LockService>();

            if (e.Value)
            {
                // Включение — нужно задать PIN
                var pin = await DisplayPromptAsync("Новый PIN-код",
                    "Введите PIN-код (4-8 цифр):", "OK", "Отмена",
                    keyboard: Keyboard.Numeric, maxLength: 8);

                if (string.IsNullOrEmpty(pin) || pin.Length < 4)
                {
                    LockSwitch.IsToggled = false;
                    return;
                }

                // Повторное подтверждение
                var confirm = await DisplayPromptAsync("Подтвердите PIN",
                    "Введите PIN-код ещё раз:", "OK", "Отмена",
                    keyboard: Keyboard.Numeric, maxLength: 8);

                if (pin != confirm)
                {
                    LockSwitch.IsToggled = false;
                    await DisplayAlertAsync("Ошибка", "PIN-коды не совпадают.", "OK");
                    return;
                }

                await lockService.SetPinAsync(pin);
                lockService.IsEnabled = true;
                ChangePinButton.IsVisible = true;
            }
            else
            {
                // Отключение — запросить текущий PIN
                var pin = await DisplayPromptAsync("Отключение блокировки",
                    "Введите текущий PIN-код:", "OK", "Отмена",
                    keyboard: Keyboard.Numeric, maxLength: 8);

                if (pin != null && await lockService.VerifyPinAsync(pin))
                {
                    lockService.IsEnabled = false;
                    ChangePinButton.IsVisible = false;
                }
                else
                {
                    LockSwitch.IsToggled = true;
                }
            }
        }

        private async void OnChangePinClicked(object? sender, EventArgs e)
        {
            var lockService = ServiceHelper.Get<LockService>();

            // Запрос текущий PIN
            var oldPin = await DisplayPromptAsync("Текущий PIN",
                "Введите текущий PIN-код:", "OK", "Отмена",
                keyboard: Keyboard.Numeric, maxLength: 8);

            if (oldPin == null || !await lockService.VerifyPinAsync(oldPin))
            {
                await DisplayAlertAsync("Ошибка", "Неверный текущий PIN-код.", "OK");
                return;
            }

            var newPin = await DisplayPromptAsync("Новый PIN-код",
                "Введите новый PIN-код (4-8 цифр):", "OK", "Отмена",
                keyboard: Keyboard.Numeric, maxLength: 8);

            if (string.IsNullOrEmpty(newPin) || newPin.Length < 4)
                return;

            var confirm = await DisplayPromptAsync("Подтвердите",
                "Введите новый PIN-код ещё раз:", "OK", "Отмена",
                keyboard: Keyboard.Numeric, maxLength: 8);

            if (newPin != confirm)
            {
                await DisplayAlertAsync("Ошибка", "PIN-коды не совпадают.", "OK");
                return;
            }

            await lockService.SetPinAsync(newPin);
            await DisplayAlertAsync("Готово", "PIN-код успешно изменён.", "OK");
        }

        // --- Семейный бюджет: экспорт/импорт JSON ---

        private async void OnShareFamilyClicked(object? sender, EventArgs e)
        {
            try
            {
                var txs = await _txService.GetTransactionsAsync();
                if (txs.Count == 0)
                {
                    await DisplayAlertAsync("Пусто", "Нет операций для экспорта.", "OK");
                    return;
                }

                var json = System.Text.Json.JsonSerializer.Serialize(txs, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                var date = DateTime.Now.ToString("yyyy-MM-dd");
                var fileName = $"budget_family_{date}.json";
                var filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);
                await File.WriteAllTextAsync(filePath, json);

                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Поделиться бюджетом",
                    File = new ShareFile(filePath)
                });
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "Экспорт семейного бюджета");
                await DisplayAlertAsync("Ошибка", ex.Message, "OK");
            }
        }

        private async void OnImportFamilyClicked(object? sender, EventArgs e)
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.Android, new[] { "application/json" } },
                        { DevicePlatform.iOS, new[] { "public.json" } },
                        { DevicePlatform.macOS, new[] { "public.json" } },
                        { DevicePlatform.WinUI, new[] { ".json" } }
                    }),
                    PickerTitle = "Выберите файл бюджета"
                });

                if (result == null)
                    return;

                var json = await File.ReadAllTextAsync(result.FullPath);
                var imported = System.Text.Json.JsonSerializer.Deserialize<List<Transaction>>(json);
                if (imported == null || imported.Count == 0)
                {
                    await DisplayAlertAsync("Ошибка", "Файл пуст или повреждён.", "OK");
                    return;
                }

                // Добавляем все импортированные операции (пропускаем дубли по дате+сумма+описание)
                var existing = await _txService.GetTransactionsAsync();
                var existingKeys = existing.Select(t => $"{t.Date:yyyyMMdd}_{t.Amount}_{t.Description}").ToHashSet();

                int added = 0;
                foreach (var tx in imported)
                {
                    var key = $"{tx.Date:yyyyMMdd}_{tx.Amount}_{tx.Description}";
                    if (!existingKeys.Contains(key))
                    {
                        tx.Id = 0; // новая запись
                        await _txService.SaveTransactionAsync(tx);
                        added++;
                    }
                }

                await DisplayAlertAsync("Готово",
                    $"Импортировано: {added} из {imported.Count}\n(дубли пропущены)", "OK");
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "Импорт семейного бюджета");
                await DisplayAlertAsync("Ошибка", ex.Message, "OK");
            }
        }
    }
}
