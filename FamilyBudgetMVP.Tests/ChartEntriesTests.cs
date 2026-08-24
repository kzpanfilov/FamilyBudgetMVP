using FamilyBudgetMVP.Models;
using FamilyBudgetMVP.Services;

namespace FamilyBudgetMVP.Tests
{
    public class ChartEntriesTests
    {
        private readonly BudgetService _service = new(new FakePalette());

        private static Transaction Tx(DateTime date, decimal amount, string category) =>
            new() { Description = "t", Amount = amount, Date = date, Category = category };

        private readonly IReadOnlyList<Category> _categories = new List<Category>
        {
            new() { Name = "Продукты",  ColorHex = "#F59E0B", MonthlyLimit = 5000 },
            new() { Name = "Транспорт", ColorHex = "#0EA5E9" }
        };

        [Fact]
        public void Only_Expenses_Of_Current_Month()
        {
            var now = DateTime.Today;
            var lastMonthDate = now.AddMonths(-1);
            var list = new[]
            {
                Tx(now, -1000, "Продукты"),
                Tx(now, 500, "Зарплата"),          // доход — не берём
                Tx(lastMonthDate, -9000, "Жилье") // прошлый месяц — не берём
            };

            var entries = _service.BuildMonthExpenseEntries(list, _categories);

            Assert.Single(entries);
            Assert.Equal("Продукты", entries[0].Label);
            Assert.Equal(1000f, entries[0].Value);
        }

        [Fact]
        public void Categories_Sorted_By_Spent_Desc()
        {
            var now = DateTime.Today;
            var list = new[]
            {
                Tx(now, -300, "Транспорт"),
                Tx(now, -2000, "Продукты")
            };

            var entries = _service.BuildMonthExpenseEntries(list, _categories);

            Assert.Equal("Продукты", entries[0].Label);
            Assert.Equal("Транспорт", entries[1].Label);
            Assert.True(entries[0].Value > entries[1].Value);
        }

        [Fact]
        public void Exceeded_Limit_Turns_Bar_Red()
        {
            var now = DateTime.Today;
            var list = new[] { Tx(now, -6000, "Продукты") }; // лимит 5000

            var entries = _service.BuildMonthExpenseEntries(list, _categories);

            Assert.Equal(SkiaSharp.SKColor.Parse("#E5484D"), entries[0].Color);
        }

        [Fact]
        public void Within_Limit_Keeps_Category_Color()
        {
            var now = DateTime.Today;
            var list = new[] { Tx(now, -4000, "Продукты") }; // лимит 5000

            var entries = _service.BuildMonthExpenseEntries(list, _categories);

            Assert.Equal(SkiaSharp.SKColor.Parse("#F59E0B"), entries[0].Color);
        }

        [Fact]
        public void No_Limit_Category_Never_Exceeded()
        {
            var now = DateTime.Today;
            var list = new[] { Tx(now, -999999, "Транспорт") }; // без лимита

            var entries = _service.BuildMonthExpenseEntries(list, _categories);

            Assert.Equal(SkiaSharp.SKColor.Parse("#0EA5E9"), entries[0].Color);
        }

        [Fact]
        public void Daily_Entries_Fill_All_Days()
        {
            var list = new[] { Tx(DateTime.Today.AddDays(-2), -100, "Продукты") };

            var entries = _service.BuildDailyExpenseEntries(list, days: 30);

            Assert.Equal(30, entries.Count);
            Assert.Equal(0f, entries[^1].Value);      // сегодня трат нет
            Assert.Equal(100f, entries[^3].Value);    // позавчера −100
        }
    }
}
