using System.Globalization;
using FamilyBudgetMVP.Helpers;
using FamilyBudgetMVP.Models;
using FamilyBudgetMVP.Services;

namespace FamilyBudgetMVP.Views
{
    /// <summary>
    /// Модалка детализации: все операции одной категории за текущий месяц.
    /// Открывается кликом по столбцу графика на дашборде.
    /// </summary>
    public partial class CategoryDetailPage : ContentPage
    {
        private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

        public CategoryDetailPage(string categoryName, List<Transaction> monthItems)
        {
            InitializeComponent();

            var category = ServiceHelper.Get<CategoryStore>().Find(categoryName);

            decimal total = -monthItems.Sum(t => t.Amount);
            string monthTitle = DateTime.Today.ToString("MMMM yyyy", RuCulture);
            monthTitle = char.ToUpper(monthTitle[0]) + monthTitle[1..];

            BindingContext = new DetailHeader
            {
                Icon = category?.Icon ?? "📦",
                TintHex = category?.TintHex ?? "#E2E8F0",
                Title = categoryName,
                MonthTitle = monthTitle,
                TotalText = $"−{total:N0} ₽",
                CountText = OperationsCountText(monthItems.Count)
            };

            ItemsList.ItemsSource = monthItems.OrderByDescending(t => t.Date).ToList();
        }

        private async void OnBackClicked(object? sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }

        // Тап по строке — редактирование операции
        private async void OnRowTapped(object? sender, EventArgs e)
        {
            if ((sender as BindableObject)?.BindingContext is Transaction tx)
            {
                var page = ServiceHelper.Get<AddTransactionPage>();
                page.SetupForEdit(tx);
                await Navigation.PushModalAsync(page);
            }
        }

        private static string OperationsCountText(int count) => count switch
        {
            1 => "1 операция",
            2 or 3 or 4 => $"{count} операции",
            _ => $"{count} операций"
        };

        private class DetailHeader
        {
            public string Icon { get; init; } = "📦";
            public string TintHex { get; init; } = "#E2E8F0";
            public string Title { get; init; } = string.Empty;
            public string MonthTitle { get; init; } = string.Empty;
            public string TotalText { get; init; } = string.Empty;
            public string CountText { get; init; } = string.Empty;
        }
    }
}
