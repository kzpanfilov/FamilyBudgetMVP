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

        private bool _isIncome;

        public AddTransactionPage(TransactionService txService, CategoryStore categories)
        {
            InitializeComponent();
            _txService = txService;
            _categories = categories;

            CategoryPicker.ItemsSource = _categories.Names;
            if (_categories.Names.Count > 0)
                CategoryPicker.SelectedIndex = _categories.Names.Count - 1; // «Разное» по умолчанию

            UpdateTypeChips();
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
                var transaction = new Transaction
                {
                    Description = DescriptionEntry.Text.Trim(),
                    Amount = _isIncome ? Math.Abs(amount) : -Math.Abs(amount),
                    Category = CategoryPicker.SelectedItem?.ToString() ?? "Разное",
                    Date = DateTime.Now
                };

                await _txService.SaveTransactionAsync(transaction);
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
                DescriptionError.IsVisible = false;
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
    }
}
