using FamilyBudgetMVP.Models;
using FamilyBudgetMVP.Services;

namespace FamilyBudgetMVP.Tests
{
    public class GroupByDayTests
    {
        private readonly BudgetService _service = new(new FakePalette());

        private static Transaction Tx(DateTime date, decimal amount, string category = "Продукты") =>
            new() { Description = "t", Amount = amount, Date = date, Category = category };

        [Fact]
        public void Today_And_Yesterday_Titles()
        {
            var today = DateTime.Today;
            var list = new[] { Tx(today.AddHours(10), -100), Tx(today.AddDays(-1), -200) };

            var groups = _service.GroupByDay(list);

            Assert.Equal(2, groups.Count);
            Assert.Equal("Сегодня", groups[0].Title);
            Assert.Equal("Вчера", groups[1].Title);
        }

        [Fact]
        public void Newest_First_By_Default()
        {
            var today = DateTime.Today;
            var list = new[]
            {
                Tx(today.AddDays(-5), -1),
                Tx(today, -2),
                Tx(today.AddDays(-2), -3)
            };

            var groups = _service.GroupByDay(list);

            Assert.Equal([today, today.AddDays(-2), today.AddDays(-5)],
                groups.Select(g => g.Items[0].Date.Date).ToList());
        }

        [Fact]
        public void Oldest_First_When_Asked()
        {
            var today = DateTime.Today;
            var list = new[]
            {
                Tx(today, -2),
                Tx(today.AddDays(-3), -1)
            };

            var groups = _service.GroupByDay(list, newestFirst: false);

            Assert.Equal(today.AddDays(-3), groups[0].Items[0].Date.Date);
        }

        [Fact]
        public void Day_Total_Signed_Formatted()
        {
            var today = DateTime.Today;
            var group = new TransactionsByDay
            {
                Title = "Сегодня",
                Items = new List<Transaction> { Tx(today, -1500), Tx(today, 300) }
            };

            // итог дня отрицательный: −1 200 ₽ (знак «−», неразрывный формат с пробелами)
            Assert.StartsWith("−", group.DayTotalText);
            Assert.EndsWith("₽", group.DayTotalText);
            Assert.DoesNotContain("+", group.DayTotalText);
        }

        [Fact]
        public void Old_Date_Title_Contains_Year()
        {
            var old = new DateTime(DateTime.Today.Year - 2, 3, 15, 12, 0, 0);
            var groups = _service.GroupByDay(new[] { Tx(old, -10) });

            Assert.Contains((DateTime.Today.Year - 2).ToString(), groups[0].Title);
        }
    }
}
