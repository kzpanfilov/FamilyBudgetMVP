using FamilyBudgetMVP.Helpers;
using FamilyBudgetMVP.Models;
using FamilyBudgetMVP.Services;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace FamilyBudgetMVP.Views
{
    /// <summary>Модальная форма добавления операции (расход или доход).</summary>
    public partial class AddTransactionPage : ContentPage
    {
        private readonly TransactionService _txService;
        private readonly CategoryStore _categories;
        private readonly NotificationService _notifications;

        private bool _isIncome;

        /// <summary>Ненулевой при редактировании существующей операции.</summary>
        private Transaction? _editing;

        public AddTransactionPage(TransactionService txService, CategoryStore categories, NotificationService notifications)
        {
            InitializeComponent();
            _txService = txService;
            _categories = categories;
            _notifications = notifications;

            // Список категорий заполняется в OnAppearing после инициализации кэша
            UpdateTypeChips();

            // Периодичность повторяющихся платежей
            RecurrencePicker.ItemsSource = Recurrence.All.Select(Recurrence.Display).ToList();
            RecurrencePicker.SelectedIndex = 0;
        }

        /// <summary>Переводит форму в режим редактирования: заполняет поля значениями операции.</summary>
        public void SetupForEdit(Transaction t)
        {
            _editing = t;
            _isIncome = t.Amount >= 0;
            UpdateTypeChips();

            DescriptionEntry.Text = t.Description;
            AmountEntry.Text = Math.Abs(t.Amount).ToString("0.##");
            SelectCategory(t.Category);

            int recIndex = Array.IndexOf(Recurrence.All, t.RecurrenceType);
            RecurrencePicker.SelectedIndex = recIndex > 0 ? recIndex : 0;
            if (t.RecurEndDate.HasValue)
                RecurEndDatePicker.Date = t.RecurEndDate.Value;

            HeaderTitle.Text = "Редактировать операцию";
        }

        private void OnRecurrenceChanged(object? sender, EventArgs e)
        {
            RecurEndWrap.IsVisible = RecurrencePicker.SelectedIndex > 0;
        }

        // --- Быстрый ввод (ТЗ MVP, этап 5) ---

        private void OnQuickExpense(object? sender, EventArgs e)
        {
            string param = (sender as Button)?.CommandParameter as string ?? string.Empty;
            var parts = param.Split('|');
            if (parts.Length != 3)
                return;

            _isIncome = false;
            UpdateTypeChips();

            DescriptionEntry.Text = parts[0];
            AmountEntry.Text = parts[1];
            SelectCategory(parts[2]);

            DescriptionError.IsVisible = false;
            AmountError.IsVisible = false;
        }

        private void SelectCategory(string name)
        {
            if (_categories.Names.Contains(name))
                CategoryPicker.SelectedItem = name;
        }

        // Автоподстановка категории по тексту описания («школа» → образование и т.п.)
        private static readonly (string[] Keywords, string Category)[] CategoryHints =
        {
            (new[] { "хлеб", "молок", "продукт", "пятёрочк", "магнит", "пекарн", "овощ", "фрукт" }, "Продукты"),
            (new[] { "автобус", "троллейбус", "трамвай", "метро", "такси", "бензин", "заправк", "проездн" }, "Транспорт"),
            (new[] { "аренд", "квартплат", "жкх", "коммунал", "ипотек", "ремонт", "электроэнерг" }, "Жилье"),
            (new[] { "кино", "театр", "ресторан", "кафе", "игр", "подписк" }, "Развлечения"),
            (new[] { "аптек", "лекарст", "таблет", "врач", "клиник", "стоматолог" }, "Здоровье")
        };

        private void TryAutoCategorize(string description)
        {
            foreach (var hint in CategoryHints)
            {
                if (hint.Keywords.Any(k => description.Contains(k, StringComparison.OrdinalIgnoreCase)))
                {
                    SelectCategory(hint.Category);
                    return;
                }
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                await _categories.InitializeAsync();
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "Форма операции: инициализация");
            }

            // Категории ещё не загружены из БД — заполнять нечем
            if (_categories.Names.Count == 0)
                return;

            // При редактировании всегда выбираем категорию исходной операции
            string? editCategory = _editing?.Category;
            int previous = CategoryPicker.SelectedIndex;
            CategoryPicker.ItemsSource = _categories.Names;

            if (editCategory != null && _categories.Names.Contains(editCategory))
                CategoryPicker.SelectedItem = editCategory;
            else
            {
                // «Разное» по умолчанию при первом открытии, выбор сохраняем при повторном
                CategoryPicker.SelectedIndex = previous >= 0 ? Math.Min(previous, _categories.Names.Count - 1)
                                                             : _categories.Names.Count - 1;
            }
        }

        private void OnExpenseTapped(object? sender, TappedEventArgs e)
        {
            _isIncome = false;
            UpdateTypeChips();
        }

        private void OnIncomeTapped(object? sender, TappedEventArgs e)
        {
            _isIncome = true;
            UpdateTypeChips();
        }

        private void UpdateTypeChips()
        {
            VisualStateManager.GoToState(ExpenseChip, _isIncome ? "Normal" : "Selected");
            VisualStateManager.GoToState(ExpenseChipLabel, _isIncome ? "Normal" : "Selected");
            VisualStateManager.GoToState(IncomeChip, _isIncome ? "Selected" : "Normal");
            VisualStateManager.GoToState(IncomeChipLabel, _isIncome ? "Selected" : "Normal");
        }

        private async void OnSaveClicked(object? sender, EventArgs e)
        {
            // Инлайн-валидация: подписи под полями вместо диалогов
            bool descOk = ValidateDescription();
            bool amountOk = ValidateAmount(out decimal amount);

            if (!descOk || !amountOk)
                return;

            try
            {
                // Базовая дата операции (для новой — сегодня)
                DateTime baseDate = (_editing?.Date ?? DateTime.Today).Date;

                var transaction = new Transaction
                {
                    Description = DescriptionEntry.Text.Trim(),
                    Amount = _isIncome ? Math.Abs(amount) : -Math.Abs(amount),
                    Category = CategoryPicker.SelectedItem?.ToString() ?? "Разное",
                    Date = _editing?.Date ?? DateTime.Now,
                    RecurrenceType = RecurrencePicker.SelectedIndex > 0
                        ? Recurrence.All[RecurrencePicker.SelectedIndex]
                        : Recurrence.None,
                    // Дата окончания по умолчанию (сегодня) = «бессрочно»:
                    // иначе неотредактированный пикер молча обнулял повторяемость
                    RecurEndDate = RecurrencePicker.SelectedIndex > 0 && RecurEndDatePicker.Date > baseDate
                        ? ((DateTime?)RecurEndDatePicker.Date)?.Date
                        : null
                };

                // Редактирование: сохраняем ключ и источник исходной записи
                if (_editing != null)
                {
                    transaction.Id = _editing.Id;
                    transaction.Source = _editing.Source;
                }

                await _txService.SaveTransactionAsync(transaction);

                // Проверяем лимиты и отправляем уведомление если нужно
                _ = Task.Run(async () =>
                {
                    try { await _notifications.CheckLimitsAndNotifyAsync(); }
                    catch { /* фоновая задача — не крашим */ }
                });

                await Navigation.PopModalAsync();
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "Сохранение операции");
                await DisplayAlertAsync("Ошибка БД", $"Не удалось сохранить: {ex.Message}", "OK");
            }
        }

        private bool ValidateDescription()
        {
            bool ok = !string.IsNullOrWhiteSpace(DescriptionEntry.Text);
            DescriptionError.IsVisible = !ok;
            return ok;
        }

        private bool ValidateAmount(out decimal amount)
        {
            // Принимаем и запятую, и точку
            string raw = (AmountEntry.Text ?? string.Empty).Trim().Replace('.', ',');

            bool ok = decimal.TryParse(raw, NumberStyles.Number,
                CultureInfo.GetCultureInfo("ru-RU"), out amount) && amount > 0;

            AmountError.IsVisible = !ok;
            return ok;
        }

        private void OnDescriptionTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.NewTextValue))
            {
                DescriptionError.IsVisible = false;
                TryAutoCategorize(e.NewTextValue);
            }
        }

        private void OnAmountTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (decimal.TryParse((e.NewTextValue ?? string.Empty).Trim().Replace('.', ','),
                    NumberStyles.Number, CultureInfo.GetCultureInfo("ru-RU"), out decimal value) && value > 0)
            {
                AmountError.IsVisible = false;
            }
        }

        private async void OnCancelClicked(object? sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }

        private async void OnVoiceClicked(object? sender, EventArgs e)
        {
            try
            {
                VoiceButton.Text = "⏹";
                VoiceButton.BackgroundColor = Colors.Red;

#if WINDOWS
                var recognizer = new Windows.Media.SpeechRecognition.SpeechRecognizer();
                var result = await recognizer.RecognizeAsync();

                VoiceButton.Text = "🎤";
                VoiceButton.BackgroundColor = Color.FromArgb("#12968A");

                if (result.Status == Windows.Media.SpeechRecognition.SpeechRecognitionResultStatus.Success)
                {
                    DescriptionEntry.Text = result.Text;
                }
#else
                VoiceButton.Text = "🎤";
                VoiceButton.BackgroundColor = Color.FromArgb("#12968A");
                await DisplayAlertAsync("Голосовой ввод",
                    "Голосовой ввод доступен на Windows. На Android будет доступен в следующем обновлении.", "OK");
#endif
            }
            catch (Exception ex)
            {
                VoiceButton.Text = "🎤";
                VoiceButton.BackgroundColor = Color.FromArgb("#12968A");
                LogService.Error(ex, "Голосовой ввод");
            }
        }
    }
}
