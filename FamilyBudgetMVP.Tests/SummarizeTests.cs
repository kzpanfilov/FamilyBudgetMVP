using FamilyBudgetMVP.Models;
using FamilyBudgetMVP.Services;

namespace FamilyBudgetMVP.Tests
{
    public class SummarizeTests
    {
        private static Transaction Tx(decimal amount) => new() { Description = "t", Amount = amount, Date = DateTime.Now };

        private readonly BudgetService _service = new(new FakePalette());

        [Fact]
        public void Empty_List_Gives_Zeroes()
        {
            var s = _service.Summarize(Array.Empty<Transaction>());

            Assert.Equal(0m, s.Balance);
            Assert.Equal(0m, s.Income);
            Assert.Equal(0m, s.Expense);
        }

        [Fact]
        public void Mixed_Transactions_Summed_Correctly()
        {
            var s = _service.Summarize(new[] { Tx(1000), Tx(-400), Tx(250), Tx(-50) });

            Assert.Equal(800m, s.Balance);
            Assert.Equal(1250m, s.Income);
            Assert.Equal(450m, s.Expense);
        }

        [Fact]
        public void Only_Expenses_Negative_Balance()
        {
            var s = _service.Summarize(new[] { Tx(-100), Tx(-200) });

            Assert.Equal(-300m, s.Balance);
            Assert.Equal(0m, s.Income);
            Assert.Equal(300m, s.Expense);
        }

        [Fact]
        public void SummarizeMonth_Weekly_Expense_Projects_Multiple_Times()
        {
            var now = DateTime.Today;
            var firstDay = new DateTime(now.Year, now.Month, 1);

            // Еженедельный расход 500₽, начат 1-го числа текущего месяца
            var tx = new Transaction
            {
                Description = "Кофе",
                Amount = -500,
                Date = firstDay,
                Category = "еда",
                RecurrenceType = Recurrence.Weekly
            };

            var s = _service.SummarizeMonth(new[] { tx }, now.Year, now.Month);

            // Должно быть минимум 3–5 вхождений (зависит от дня месяца)
            Assert.True(s.Expense > 500, $"Expense {s.Expense} should be > 500 for weekly recurring");
            Assert.True(s.Expense <= 500 * 6, $"Expense {s.Expense} should be <= 6 weeks max");
        }

        [Fact]
        public void SummarizeMonth_Monthly_Expense_One_Time_Per_Month()
        {
            var now = DateTime.Today;
            var firstDay = new DateTime(now.Year, now.Month, 10);

            var tx = new Transaction
            {
                Description = "Аренда",
                Amount = -30000,
                Date = firstDay,
                Category = "жильё",
                RecurrenceType = Recurrence.Monthly
            };

            var s = _service.SummarizeMonth(new[] { tx }, now.Year, now.Month);

            Assert.Equal(30000m, s.Expense);
        }

        [Fact]
        public void SummarizeMonth_Quarterly_Expense_Counts_Once_Per_Quarter()
        {
            var now = DateTime.Today;
            var firstDay = new DateTime(now.Year, now.Month, 1);

            var tx = new Transaction
            {
                Description = "Квартальный",
                Amount = -9000,
                Date = firstDay,
                Category = "прочее",
                RecurrenceType = Recurrence.Quarterly
            };

            var s = _service.SummarizeMonth(new[] { tx }, now.Year, now.Month);

            Assert.Equal(9000m, s.Expense);
        }

        [Fact]
        public void SummarizeMonth_NonRecurring_Old_Transaction_Not_Counted()
        {
            var now = DateTime.Today;
            var oldDate = now.AddMonths(-3);

            var tx = new Transaction
            {
                Description = "Старый",
                Amount = -1000,
                Date = oldDate,
                Category = "прочее"
            };

            var s = _service.SummarizeMonth(new[] { tx }, now.Year, now.Month);

            Assert.Equal(0m, s.Expense);
        }
    }
}
