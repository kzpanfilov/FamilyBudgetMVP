using FamilyBudgetMVP.Models;
using FamilyBudgetMVP.Services;

namespace FamilyBudgetMVP.Tests
{
    /// <summary>Дополнительные тесты BudgetService: краевые случаи и Edge Cases.</summary>
    public class BudgetServiceAdditionalTests
    {
        private readonly BudgetService _service = new(new FakePalette());

        private static Transaction Tx(DateTime date, decimal amount, string category = "Продукты") =>
            new() { Description = "t", Amount = amount, Date = date, Category = category };

        [Fact]
        public void Summarize_Only_Income()
        {
            var s = _service.Summarize(new[] { Tx(DateTime.Now, 1000), Tx(DateTime.Now, 500) });
            Assert.Equal(1500m, s.Income);
            Assert.Equal(0m, s.Expense);
            Assert.Equal(1500m, s.Balance);
        }

        [Fact]
        public void GroupByDay_Multiple_Items_Same_Day()
        {
            var today = DateTime.Today;
            var list = new[]
            {
                Tx(today.AddHours(10), -100),
                Tx(today.AddHours(14), -200),
                Tx(today.AddHours(8), -50)
            };

            var groups = _service.GroupByDay(list);

            Assert.Single(groups);
            Assert.Equal(3, groups[0].Items.Count);
        }

        [Fact]
        public void BuildMonthExpenseEntries_Custom_Year_Month()
        {
            var list = new[]
            {
                Tx(new DateTime(2025, 6, 15), -500, "Продукты"),
                Tx(new DateTime(2025, 6, 20), -300, "Транспорт"),
                Tx(new DateTime(2025, 7, 1), -100, "Продукты")
            };

            var entries = _service.BuildMonthExpenseEntries(list, year: 2025, month: 6);

            Assert.Equal(2, entries.Count);
            Assert.Equal("Продукты", entries[0].Label);
        }

        [Fact]
        public void BuildDailyExpenseEntries_All_Zeros_When_No_Expenses()
        {
            var entries = _service.BuildDailyExpenseEntries(Array.Empty<Transaction>(), days: 7);

            Assert.Equal(7, entries.Count);
            Assert.All(entries, e => Assert.Equal(0f, e.Value));
        }

        [Fact]
        public void CheckMonthlyLimits_Empty_Categories_Returns_Empty()
        {
            var statuses = _service.CheckMonthlyLimits(
                new[] { Tx(DateTime.Today, -5000) },
                Array.Empty<Category>());

            Assert.Empty(statuses);
        }

        [Fact]
        public void GroupByDay_Empty_List_Returns_Empty()
        {
            var groups = _service.GroupByDay(Array.Empty<Transaction>());
            Assert.Empty(groups);
        }

        [Fact]
        public void Summarize_Zero_Amount_Is_Neither_Income_Nor_Expense()
        {
            var s = _service.Summarize(new[] { Tx(DateTime.Now, 0) });
            Assert.Equal(0m, s.Income);
            Assert.Equal(0m, s.Expense);
        }

        [Fact]
        public void IsLimitExceeded_Returns_False_When_No_Categories()
        {
            Assert.False(BudgetService.IsLimitExceeded("Продукты", 100000, null));
        }
    }
}
