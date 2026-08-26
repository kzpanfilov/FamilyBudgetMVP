using System.Collections.ObjectModel;
using FamilyBudgetMVP.Helpers;
using FamilyBudgetMVP.Models;
using FamilyBudgetMVP.Services;
using Microsoft.Maui.Controls;

namespace FamilyBudgetMVP.Views
{
    /// <summary>Полная история операций с фильтрами и сортировкой.</summary>
    public partial class HistoryPage : ContentPage
    {
        private readonly TransactionService _txService = ServiceHelper.Get<TransactionService>();
        private readonly BudgetService _budgetService = ServiceHelper.Get<BudgetService>();
        private readonly CategoryStore _categories = ServiceHelper.Get<CategoryStore>();

        private List<Transaction> _all = new();

        // null = все категории; 0 = всё время
        private string? _categoryFilter;
        private int _periodDays;

        public HistoryPage()
        {
            InitializeComponent();

            CategoryFilterPicker.ItemsSource = new List<string> { "Все категории" }
                .Concat(_categories.Names)
                .ToList();
            CategoryFilterPicker.SelectedIndex = 0;

            SortPicker.SelectedIndex = 0;
            UpdatePeriodChips();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await ReloadAsync();
        }

        private async Task ReloadAsync()
        {
            try
            {
                // Гарантируем готовность БД и кэша категорий (идемпотентно)
                await _txService.InitializeAsync();
                await _categories.InitializeAsync();

                _all = await _txService.GetTransactionsAsync();
                ApplyFilters();
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "История: загрузка");
                SummaryLabel.Text = "Ошибка загрузки данных";
            }
        }

        // --- Фильтры ---

        private void OnFilterChanged(object? sender, EventArgs e)
        {
            _categoryFilter = CategoryFilterPicker.SelectedIndex > 0
                ? CategoryFilterPicker.SelectedItem?.ToString()
                : null;
            ApplyFilters();
        }

        private void OnSortChanged(object? sender, EventArgs e) => ApplyFilters();

        private void OnSearchTextChanged(object? sender, TextChangedEventArgs e) => ApplyFilters();

        private void OnPeriodTapped(object? sender, TappedEventArgs e)
        {
            _periodDays = e.Parameter is int days ? days : 0;

            UpdatePeriodChips();
            ApplyFilters();
        }

        private void UpdatePeriodChips()
        {
            SetPeriodState(PeriodChipAll, PeriodChipAllLabel, _periodDays == 0);
            SetPeriodState(PeriodChip30, PeriodChip30Label, _periodDays == 30);
            SetPeriodState(PeriodChip7, PeriodChip7Label, _periodDays == 7);
        }

        private static void SetPeriodState(Border chip, Label label, bool selected)
        {
            string state = selected ? "Selected" : "Normal";
            VisualStateManager.GoToState(chip, state);
            VisualStateManager.GoToState(label, state);
        }

        private void ApplyFilters()
        {
            IEnumerable<Transaction> query = _all;

            // Поиск по описанию (регистронезависимый)
            string search = SearchEntry.Text?.Trim() ?? string.Empty;
            if (search.Length > 0)
                query = query.Where(t => t.Description.Contains(search, StringComparison.OrdinalIgnoreCase));

            if (_categoryFilter != null)
                query = query.Where(t => t.Category == _categoryFilter);

            if (_periodDays > 0)
            {
                var from = DateTime.Today.AddDays(-(_periodDays - 1));
                query = query.Where(t => t.Date.Date >= from);
            }

            var filtered = query.ToList();

            int count = filtered.Count;
            decimal net = filtered.Sum(t => t.Amount);
            SummaryLabel.Text = count == 0
                ? "Нет операций по выбранным условиям"
                : $"{count} {(count % 10 == 1 && count % 100 != 11 ? "операция" : "операций")} • {(net >= 0 ? "+" : "−")}{Math.Abs(net):N0} ₽";

            var list = TransactionsList;

            // Плоские строки через DataTemplateSelector вместо IsGrouped:
            // группировочный обработчик WinUI нестабилен (NRE в CVS)
            var rows = new List<HistoryRow>();

            switch (SortPicker.SelectedIndex)
            {
                case 1: // сначала старые
                    foreach (var g in _budgetService.GroupByDay(filtered, newestFirst: false))
                        AddDayRows(rows, g);
                    break;

                case 2: // по сумме: самые крупные расходы сверху
                    rows.AddRange(filtered
                        .OrderBy(t => t.Amount)
                        .ThenByDescending(t => t.Date)
                        .Select(t => new HistoryTransactionRow { Transaction = t }));
                    break;

                default: // сначала новые
                    foreach (var g in _budgetService.GroupByDay(filtered))
                        AddDayRows(rows, g);
                    break;
            }

            list.ItemsSource = rows;
        }

        private static void AddDayRows(List<HistoryRow> rows, TransactionsByDay group)
        {
            rows.Add(new HistoryDayHeader { Title = group.Title, DayTotalText = group.DayTotalText });
            rows.AddRange(group.Items.Select(t => new HistoryTransactionRow { Transaction = t }));
        }

        // --- Удаление (как на дашборде) ---

        private static Transaction? GetRowTransaction(object? sender)
        {
            return (sender as BindableObject)?.BindingContext switch
            {
                HistoryTransactionRow row => row.Transaction,
                Transaction tx => tx,
                _ => null
            };
        }

        private async void OnDeleteTransactionClicked(object? sender, EventArgs e)
        {
            if (GetRowTransaction(sender) is { } transaction)
                await TryDeleteTransactionAsync(transaction);
        }

        private async void OnDeleteTransactionTapped(object? sender, TappedEventArgs e)
        {
            if (GetRowTransaction(sender) is { } transaction)
                await TryDeleteTransactionAsync(transaction);
        }

        private async Task TryDeleteTransactionAsync(Transaction transaction)
        {
            bool confirm = await DisplayAlertAsync("Подтверждение", $"Удалить запись: {transaction.Description}?", "Да", "Нет");
            if (!confirm) return;

            try
            {
                await _txService.DeleteTransactionAsync(transaction.Id);
                _all.Remove(transaction);
                ApplyFilters();
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "История: удаление");
                await DisplayAlertAsync("Ошибка", $"Не удалось удалить: {ex.Message}", "OK");
            }
        }

        private void OnRowPointerEntered(object? sender, PointerEventArgs e)
        {
            if (sender is Grid row)
                foreach (var child in row.Children.OfType<Button>())
                    child.IsVisible = true;
        }

        private void OnRowPointerExited(object? sender, PointerEventArgs e)
        {
            if (sender is Grid row)
                foreach (var child in row.Children.OfType<Button>())
                    child.IsVisible = false;
        }

        // Тап по строке — редактирование операции
        private async void OnRowTapped(object? sender, EventArgs e)
        {
            if (GetRowTransaction(sender) is { } transaction)
            {
                var page = ServiceHelper.Get<AddTransactionPage>();
                page.SetupForEdit(transaction);
                await Navigation.PushModalAsync(page);
            }
        }
    }
}
