using FamilyBudgetMVP.Models;
using FamilyBudgetMVP.Services;

namespace FamilyBudgetMVP.Tests
{
    /// <summary>Месячные лимиты: превышение, порог 85%, отсутствие лимита.</summary>
    public class LimitTests
    {
        private readonly BudgetService _service = new(new FakePalette());

        private static Transaction Expense(int dayOfMonth, decimal amount, string category)
        {
            var now = DateTime.Today;
            int day = Math.Min(dayOfMonth, DateTime.DaysInMonth(now.Year, now.Month));
            return new Transaction
            {
                Description = "t",
                Amount = -amount,
                Date = new DateTime(now.Year, now.Month, day),
                Category = category
            };
        }

        private static Transaction Income(decimal amount) => Expense(1, -amount, "Прочее");

        private readonly IReadOnlyList<Category> _categories = new List<Category>
        {
            new() { Name = "Продукты",  ColorHex = "#F59E0B", MonthlyLimit = 10000 },
            new() { Name = "Транспорт", ColorHex = "#0EA5E9", MonthlyLimit = 2000 },
            new() { Name = "Развлечения", ColorHex = "#EC4899" } // без лимита
        };

        [Fact]
        public void Exceeded_When_Spent_Above_Limit()
        {
            var list = new[] { Expense(5, 12000, "Продукты") }; // лимит 10000

            var statuses = _service.CheckMonthlyLimits(list, _categories);

            var s = Assert.Single(statuses);
            Assert.Equal("Продукты", s.Category);
            Assert.True(s.Exceeded);
        }

        [Fact]
        public void Approaching_At_85_Percent()
        {
            // 8800 из 10000 = ровно 88% — «близко к лимиту», но не превышение
            var list = new[] { Expense(3, 8800, "Продукты") };

            var statuses = _service.CheckMonthlyLimits(list, _categories);

            var s = Assert.Single(statuses);
            Assert.False(s.Exceeded);
            Assert.True(s.Approaching);
        }

        [Fact]
        public void Quiet_When_Below_Threshold()
        {
            // 5000 из 10000 = 50% — в список предупреждений не попадает
            var list = new[]
            {
                Expense(2, 3000, "Продукты"),
                Expense(4, 2000, "Продукты"),
                Income(5000) // доходы не влияют на расход по категории
            };

            var statuses = _service.CheckMonthlyLimits(list, _categories);

            Assert.Empty(statuses);
        }

        [Fact]
        public void No_Limit_Category_Is_Never_Reported()
        {
            var list = new[] { Expense(6, 999999, "Развлечения") };

            Assert.Empty(_service.CheckMonthlyLimits(list, _categories));
        }

        [Fact]
        public void Multiple_Categories_Reported_Together()
        {
            var list = new[]
            {
                Expense(5, 2500, "Транспорт"),   // превышен (лимит 2000)
                Expense(7, 9000, "Продукты")     // 90% — близко к лимиту
            };

            var statuses = _service.CheckMonthlyLimits(list, _categories);

            Assert.Equal(2, statuses.Count);
            Assert.Contains(statuses, s => s.Category == "Транспорт" && s.Exceeded);
            Assert.Contains(statuses, s => s.Category == "Продукты" && s.Approaching);
        }

        [Theory]
        [InlineData(9999, false)]  // на рубль ниже лимита
        [InlineData(10000, false)] // ровно лимит — не превышение
        [InlineData(10001, true)]  // на рубль выше
        public void IsLimitExceeded_Boundary(decimal spent, bool expected)
        {
            Assert.Equal(expected, BudgetService.IsLimitExceeded("Продукты", spent, _categories));
        }
    }
}
