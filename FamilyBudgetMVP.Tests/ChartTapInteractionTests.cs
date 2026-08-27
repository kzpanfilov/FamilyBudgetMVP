using FamilyBudgetMVP.Models;
using FamilyBudgetMVP.Services;

namespace FamilyBudgetMVP.Tests
{
    public class ChartTapInteractionTests
    {
        private readonly BudgetService _service = new(new FakePalette());

        private static Transaction Tx(DateTime date, decimal amount, string category,
            string recurrence = Recurrence.None) =>
            new()
            {
                Description = "t",
                Amount = amount,
                Date = date,
                Category = category,
                RecurrenceType = recurrence
            };

        private static IReadOnlyList<Category> Categories => new List<Category>
        {
            new() { Name = "Продукты", ColorHex = "#F59E0B", MonthlyLimit = 5000 },
            new() { Name = "Транспорт", ColorHex = "#0EA5E9" },
            new() { Name = "Жильё", ColorHex = "#8B5CF6", MonthlyLimit = 30000 }
        };

        [Fact]
        public void Tapping_Bar_With_No_Expenses_Returns_Empty_Detail()
        {
            var now = DateTime.Today;
            var list = new[] { Tx(now, -1000, "Продукты") };

            var items = _service.FilterByCategory(list, "Транспорт");

            Assert.Empty(items);
        }

        [Fact]
        public void Detail_List_Excludes_Income()
        {
            var now = DateTime.Today;
            var list = new[]
            {
                Tx(now, -2000, "Продукты"),
                Tx(now, 5000, "Зарплата"),
                Tx(now, -500, "Продукты")
            };

            var items = _service.FilterByCategory(list, "Продукты");

            Assert.All(items, t => Assert.True(t.Amount < 0));
            Assert.Equal(2, items.Count);
        }

        [Fact]
        public void Detail_List_Excludes_Other_Months()
        {
            var now = DateTime.Today;
            var prevMonth = now.AddMonths(-1);
            var list = new[]
            {
                Tx(now, -1000, "Продукты"),
                Tx(prevMonth, -2000, "Продукты")
            };

            var items = _service.FilterByCategory(list, "Продукты");

            Assert.Single(items);
            Assert.Equal(-1000m, items[0].Amount);
        }

        [Fact]
        public void Detail_Total_Matches_Chart_Value()
        {
            var now = DateTime.Today;
            var list = new[]
            {
                Tx(now, -500, "Еда"),
                Tx(now, -1500, "Еда"),
                Tx(now, -300, "Еда"),
                Tx(now, -800, "Транспорт")
            };

            var entries = _service.BuildMonthExpenseEntries(list, Categories);
            var foodEntry = entries.First(e => e.Label == "Еда");
            var foodItems = _service.FilterByCategory(list, "Еда");

            Assert.Equal(2300f, foodEntry.Value);
            Assert.Equal(2300m, -foodItems.Sum(t => t.Amount));
        }

        [Fact]
        public void Multiple_Categories_Each_Filtered_Independently()
        {
            var now = DateTime.Today;
            var list = new[]
            {
                Tx(now, -100, "А"),
                Tx(now, -200, "Б"),
                Tx(now, -300, "А"),
                Tx(now, -400, "В"),
                Tx(now, -500, "Б")
            };

            Assert.Equal(2, _service.FilterByCategory(list, "А").Count);
            Assert.Equal(2, _service.FilterByCategory(list, "Б").Count);
            Assert.Single(_service.FilterByCategory(list, "В"));
            Assert.Empty(_service.FilterByCategory(list, "Г"));
        }

        [Fact]
        public void Empty_Transactions_Gives_Empty_Chart_And_Empty_Detail()
        {
            var entries = _service.BuildMonthExpenseEntries(
                Array.Empty<Transaction>(), Categories);
            var items = _service.FilterByCategory(
                Array.Empty<Transaction>(), "Продукты");

            Assert.Empty(entries);
            Assert.Empty(items);
        }

        [Fact]
        public void Single_Expense_Chart_And_Detail_Match()
        {
            var now = DateTime.Today;
            var list = new[] { Tx(now, -7777, "Жильё") };

            var entries = _service.BuildMonthExpenseEntries(list, Categories);
            var items = _service.FilterByCategory(list, "Жильё");

            Assert.Single(entries);
            Assert.Equal(7777f, entries[0].Value);
            Assert.Single(items);
            Assert.Equal(-7777m, items[0].Amount);
        }

        [Fact]
        public void SummarizeMonth_Empty_List_Returns_Zeroes()
        {
            var s = _service.SummarizeMonth(Array.Empty<Transaction>());

            Assert.Equal(0m, s.Balance);
            Assert.Equal(0m, s.Income);
            Assert.Equal(0m, s.Expense);
        }

        [Fact]
        public void SummarizeMonth_Only_In_Other_Month_Returns_Zeroes()
        {
            var now = DateTime.Today;
            var old = now.AddMonths(-2);
            var list = new[] { Tx(old, 5000, "Зарплата"), Tx(old, -1000, "Еда") };

            var s = _service.SummarizeMonth(list, now.Year, now.Month);

            Assert.Equal(0m, s.Balance);
            Assert.Equal(0m, s.Income);
            Assert.Equal(0m, s.Expense);
        }

        [Fact]
        public void SummarizeMonth_Income_And_Expense_Separated()
        {
            var now = DateTime.Today;
            var list = new[]
            {
                Tx(now, 100000, "Зарплата"),
                Tx(now, -30000, "Жильё"),
                Tx(now, -15000, "Продукты"),
                Tx(now, -5000, "Транспорт")
            };

            var s = _service.SummarizeMonth(list, now.Year, now.Month);

            Assert.Equal(50000m, s.Balance);
            Assert.Equal(100000m, s.Income);
            Assert.Equal(50000m, s.Expense);
        }
    }
}
