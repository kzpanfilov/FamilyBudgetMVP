using FamilyBudgetMVP.Models;
using FamilyBudgetMVP.Services;

namespace FamilyBudgetMVP.Tests
{
    public class FilterByCategoryTests
    {
        private readonly BudgetService _service = new(new FakePalette());

        private static Transaction Tx(DateTime date, decimal amount, string category) =>
            new() { Description = "t", Amount = amount, Date = date, Category = category };

        [Fact]
        public void Returns_Only_Expenses_Of_Given_Category_And_Month()
        {
            var now = DateTime.Today;
            var list = new[]
            {
                Tx(now, -1000, "Продукты"),
                Tx(now, -500, "Транспорт"),
                Tx(now, 500, "Зарплата"),
                Tx(now, -300, "Продукты"),
                Tx(now.AddMonths(-1), -9000, "Продукты")
            };

            var result = _service.FilterByCategory(list, "Продукты");

            Assert.Equal(2, result.Count);
            Assert.All(result, t => Assert.Equal("Продукты", t.Category));
            Assert.All(result, t => Assert.True(t.Amount < 0));
        }

        [Fact]
        public void Empty_When_No_Matching_Transactions()
        {
            var now = DateTime.Today;
            var list = new[]
            {
                Tx(now, -1000, "Транспорт")
            };

            var result = _service.FilterByCategory(list, "Продукты");

            Assert.Empty(result);
        }

        [Fact]
        public void Empty_List_Gives_Empty()
        {
            var result = _service.FilterByCategory(Array.Empty<Transaction>(), "Продукты");

            Assert.Empty(result);
        }

        [Fact]
        public void Filters_By_Specific_Month()
        {
            var now = DateTime.Today;
            var lastMonth = now.AddMonths(-1);
            var list = new[]
            {
                Tx(now, -1000, "Продукты"),
                Tx(lastMonth, -2000, "Продукты")
            };

            var result = _service.FilterByCategory(list, "Продукты", now.Year, now.Month);

            Assert.Single(result);
            Assert.Equal(-1000m, result[0].Amount);
        }

        [Fact]
        public void Sorted_By_Date_Descending()
        {
            var now = DateTime.Today;
            var list = new[]
            {
                Tx(now.AddDays(-10), -100, "Продукты"),
                Tx(now, -300, "Продукты"),
                Tx(now.AddDays(-5), -200, "Продукты")
            };

            var result = _service.FilterByCategory(list, "Продукты");

            Assert.Equal(-300m, result[0].Amount);
            Assert.Equal(-200m, result[1].Amount);
            Assert.Equal(-100m, result[2].Amount);
        }

        [Fact]
        public void Excludes_Income_Transactions()
        {
            var now = DateTime.Today;
            var list = new[]
            {
                Tx(now, 5000, "Зарплата"),
                Tx(now, -500, "Продукты"),
                Tx(now, 1000, "Фриланс")
            };

            var result = _service.FilterByCategory(list, "Продукты");

            Assert.Single(result);
        }

        [Fact]
        public void ChartTap_Flow_Returns_Correct_Category_Items()
        {
            var now = DateTime.Today;
            var list = new[]
            {
                Tx(now, -1500, "Еда"),
                Tx(now, -800, "Транспорт"),
                Tx(now, -300, "Еда"),
                Tx(now, -2000, "Жильё")
            };

            var chartEntries = _service.BuildMonthExpenseEntries(list,
                new List<Category>
                {
                    new() { Name = "Еда", ColorHex = "#F59E0B" },
                    new() { Name = "Транспорт", ColorHex = "#0EA5E9" },
                    new() { Name = "Жильё", ColorHex = "#8B5CF6" }
                });

            Assert.Equal(3, chartEntries.Count);

            // Жильё — самый большой расход (2000), поэтому entries[0]
            var tappedCategory = chartEntries[0].Label;
            var detailItems = _service.FilterByCategory(list, tappedCategory);

            Assert.Equal("Жильё", tappedCategory);
            Assert.Single(detailItems);
            Assert.Equal(2000m, -detailItems[0].Amount);
        }
    }
}
