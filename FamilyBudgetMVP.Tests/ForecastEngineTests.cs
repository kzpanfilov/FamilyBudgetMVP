using FamilyBudgetMVP.Models;
using FamilyBudgetMVP.Services;

namespace FamilyBudgetMVP.Tests
{
    /// <summary>
    /// Прогноз остатка: дата исчерпания бюджета, повторяющиеся платежи,
    /// крайние случаи из ТЗ (нулевой доход, крупные траты, пересечения дат).
    /// </summary>
    public class ForecastEngineTests
    {
        private static readonly DateTime AsOf = new(2026, 8, 24);

        private static Transaction Income(decimal amount, DateTime date, string recurrence = Recurrence.None, DateTime? end = null) =>
            new() { Description = "доход", Amount = amount, Date = date, Category = "Доход", RecurrenceType = recurrence, RecurEndDate = end };

        private static Transaction Expense(decimal amount, DateTime date, string recurrence = Recurrence.None, DateTime? end = null) =>
            new() { Description = "расход", Amount = -amount, Date = date, Category = "Прочее", RecurrenceType = recurrence, RecurEndDate = end };

        [Fact]
        public void ZeroIncome_BurnsDown_And_RunsOut()
        {
            // Нулевой доход: 30000 потрачено за последние 30 дней → ~1000/день,
            // баланс на сегодня 40000 − 30000 = 10000
            var txs = new List<Transaction>
            {
                Income(40000, AsOf.AddDays(-40)),   // вне окна burn, но в балансе
                Expense(30000, AsOf.AddDays(-15))
            };

            var f = ForecastEngine.Project(txs, AsOf);

            Assert.Equal(10000, f.StartBalance);
            Assert.Equal(1000m, f.DailyBurn);
            Assert.True(f.RunsOut);
            // 10000 / 1000: ноль 03.09, минус впервые 04.09
            Assert.Equal(new DateTime(2026, 9, 4), f.RunoutDate);
        }

        [Fact]
        public void BigExpense_CoveredByIncome_DoesNotRunOut()
        {
            var txs = new List<Transaction>
            {
                Income(25000, AsOf),
                Expense(20000, AsOf.AddDays(-31))   // крупная трата вне окна burn
            };
            txs.Add(Income(5000, AsOf));             // баланс: 25000 + 5000 − 20000 = 10000

            var f = ForecastEngine.Project(txs, AsOf, horizonDays: 10);

            Assert.False(f.RunsOut);
            Assert.Equal(10000, f.HorizonEndBalance);
        }

        [Fact]
        public void MonthlyRecurring_SalaryAndRent_Overlap_CountedOnceEach()
        {
            // Зарплата 25-го и аренда 25-го: пересечение дат — оба должны сработать
            var txs = new List<Transaction>
            {
                Income(60000, new DateTime(2026, 7, 25), Recurrence.Monthly),
                Expense(30000, new DateTime(2026, 7, 25), Recurrence.Monthly)
            };

            var f = ForecastEngine.Project(txs, new DateTime(2026, 8, 26), horizonDays: 35);

            // Стартовый баланс +30000 (базовые вхождения от 25.07) и 25.09 ещё +60000 − 30000;
            // вхождение в сам день asOf не учитывается — оно войдёт в баланс при вводе операции
            Assert.Equal(60000, f.HorizonEndBalance);
            Assert.False(f.RunsOut);
        }

        [Fact]
        public void WeeklyRecurrence_Accrues_EverySevenDays()
        {
            var txs = new List<Transaction> { Income(1000, new DateTime(2026, 8, 3), Recurrence.Weekly) };

            var f = ForecastEngine.Project(txs, AsOf, horizonDays: 7);

            // Стартовый баланс 1000 (базовое вхождение от 03.08) + 1000 за 31.08
            Assert.Equal(2000, f.HorizonEndBalance);
        }

        [Fact]
        public void QuarterlyRecurrence_FiresEveryThreeMonths()
        {
            var txs = new List<Transaction> { Income(9000, new DateTime(2026, 6, 1), Recurrence.Quarterly) };

            // Горизонт 100 дней захватывает два вхождения: 01.09 и 01.12
            var f = ForecastEngine.Project(txs, AsOf, horizonDays: 100);

            // Базовое (01.06) в балансе + два будущих
            Assert.Equal(27000, f.HorizonEndBalance);
        }

        [Fact]
        public void RecurEndDate_StopsOccurrences()
        {
            var txs = new List<Transaction>
            {
                Expense(5000, new DateTime(2026, 7, 20), Recurrence.Monthly,
                        end: new DateTime(2026, 8, 20))    // последняя списательная дата — август
            };
            txs.Add(Income(50000, AsOf));

            var f = ForecastEngine.Project(txs, AsOf, horizonDays: 40);

            // Списания после EndDate нет; баланс: −5000 (базовое) + 50000 = 45000
            Assert.Equal(45000, f.HorizonEndBalance);
        }

        [Fact]
        public void RunwayText_FormatsRunoutDate()
        {
            var txs = new List<Transaction> { Income(1000, AsOf.AddDays(-5)), Expense(1000, AsOf.AddDays(-2)) };

            var f = ForecastEngine.Project(txs, AsOf);

            Assert.Contains("Денег хватит до", f.RunwayText);
        }

        [Fact]
        public void ProjectMonth_HorizonIsMonthEnd()
        {
            var f = ForecastEngine.ProjectMonth(new List<Transaction>(), AsOf);

            Assert.Equal(new DateTime(2026, 8, 31), f.HorizonEnd);
        }
    }
}
