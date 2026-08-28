using FamilyBudgetMVP.Models;
using FamilyBudgetMVP.Services;

namespace FamilyBudgetMVP.Tests
{
    /// <summary>
    /// Сквозная симуляция пользовательского сценария на уровне сервисов:
    /// что пользователь видит на дашборде/графике/лимитах/прогнозе при
    /// заданном наборе операций и кастомном периоде бюджета (21 → 4).
    /// </summary>
    public class UserJourneySimulationTests
    {
        private static readonly DateTime AsOf = new(2026, 8, 27);
        private readonly BudgetService _service = new(new FakePalette());

        // Период бюджета: с 21-го числа по 4-е следующего (21 август … 5 сентября, эксклюзив)
        private static readonly BudgetPeriod Period = new(21, 4);

        private static Transaction Exp(string cat, decimal amount, DateTime date, string recurrence = Recurrence.None, DateTime? end = null) =>
            new() { Description = cat, Amount = -amount, Date = date, Category = cat, RecurrenceType = recurrence, RecurEndDate = end };

        private static Transaction Inc(decimal amount, DateTime date, string recurrence = Recurrence.None, DateTime? end = null) =>
            new() { Description = "Доход", Amount = amount, Date = date, Category = "Доход", RecurrenceType = recurrence, RecurEndDate = end };

        private static List<Transaction> Fixture()
        {
            var (start, endExclusive) = Period.Resolve(AsOf);
            Assert.Equal(new DateTime(2026, 8, 21), start);
            Assert.Equal(new DateTime(2026, 9, 5), endExclusive);

            return new List<Transaction>
            {
                Inc(50000, new DateTime(2026, 8, 1)),                      // остаток с прошлого периода — вне окна
                Inc(60000, new DateTime(2026, 8, 21), Recurrence.Monthly), // зарплата 21-го
                Exp("Жилье", 30000, new DateTime(2026, 8, 21), Recurrence.Monthly), // аренда 21-го
                Exp("Продукты", 2000, new DateTime(2026, 8, 22)),
                Exp("Здоровье", 2500, new DateTime(2026, 8, 23)),
                Exp("Транспорт", 1000, new DateTime(2026, 8, 24)),
                Exp("Продукты", 4500, new DateTime(2026, 8, 26)),
                Exp("Транспорт", 800, new DateTime(2026, 8, 28)),
                Exp("Продукты", 1500, new DateTime(2026, 9, 2))
            };
        }

        // --- Шаг 1: шапка дашборда «Баланс / Доходы / Расходы» за период ---

        [Fact]
        public void Dashboard_BalanceIncomeExpense_Over_CustomPeriod()
        {
            var (start, endExclusive) = Period.Resolve(AsOf);
            var s = _service.SummarizeRange(Fixture(), start, endExclusive);

            // Доход: только зарплата 21.08 (следующая 21.09 — вне окна)
            Assert.Equal(60000m, s.Income);
            // Расход: аренда 30000 + продукты 8000 + транспорт 1800 + здоровье 2500
            Assert.Equal(42300m, s.Expense);
            // Баланс периода
            Assert.Equal(17700m, s.Balance);
        }

        // --- Шаг 2: операции 20.08 (вне периода) и 04.09 (последний день) считаются правильно ---

        [Fact]
        public void Period_Boundaries_ExcludeBefore_IncludeLastDay()
        {
            var (start, endExclusive) = Period.Resolve(AsOf);
            var txs = new[]
            {
                Exp("Продукты", 999, new DateTime(2026, 8, 20)), // до старта — не считается
                Exp("Продукты", 111, new DateTime(2026, 9, 4)),  // последний день — считается
                Exp("Продукты", 222, new DateTime(2026, 9, 5))   // за окном — не считается
            };

            var s = _service.SummarizeRange(txs, start, endExclusive);

            Assert.Equal(111m, s.Expense);
        }

        // --- Шаг 3: график категорий (столбцы и перекраска при превышении лимита) ---

        [Fact]
        public void CategoryChart_ColumnsAndExceededTint()
        {
            var (start, endExclusive) = Period.Resolve(AsOf);
            var categories = new[]
            {
                new Category { Name = "Продукты", ColorHex = "#F59E0B", MonthlyLimit = 10000 },
                new Category { Name = "Транспорт", ColorHex = "#0EA5E9", MonthlyLimit = 1500 },
                new Category { Name = "Здоровье", ColorHex = "#22C55E" }
            };

            var entries = _service.BuildRangeExpenseEntries(Fixture(), start, endExclusive, categories);

            Assert.Equal(4, entries.Count); // Жилье, Продукты, Транспорт, Здоровье
            var transport = entries.First(e => e.Label == "Транспорт");

            // Транспорт потратил 1800 из лимита 1500 — столбец красный
            Assert.Equal(SkiaSharp.SKColor.Parse("#E5484D"), transport.Color);

            var products = entries.First(e => e.Label == "Продукты");
            Assert.Equal(SkiaSharp.SKColor.Parse("#F59E0B"), products.Color); // в пределах лимита — фирменный цвет
        }

        // --- Шаг 4: предупреждение о лимитах на дашборде ---

        [Fact]
        public void LimitWarnings_ExceededAndApproaching()
        {
            var (start, endExclusive) = Period.Resolve(AsOf);
            var categories = new[]
            {
                new Category { Name = "Транспорт", MonthlyLimit = 1500 },  // 1800 > 1500 → превышение
                new Category { Name = "Продукты", MonthlyLimit = 9000 },   // 8000 ≥ 7650 → на подходе
                new Category { Name = "Здоровье", MonthlyLimit = 5000 }    // 2500 < 4250 → тихо
            };

            var issues = _service.CheckLimitsInRange(Fixture(), categories, start, endExclusive);

            var transport = issues.Single(i => i.Category == "Транспорт");
            Assert.True(transport.Exceeded);
            Assert.Equal(300m, transport.Spent - transport.Limit);

            var products = issues.Single(i => i.Category == "Продукты");
            Assert.False(products.Exceeded);
            Assert.True(products.Approaching);
        }

        // --- Шаг 5: детализация категории после клика по столбцу графика ---

        [Fact]
        public void CategoryDetail_FiltersOnlyThatCategory_NewestFirst()
        {
            var (start, endExclusive) = Period.Resolve(AsOf);
            var items = _service.FilterByCategoryRange(Fixture(), "Продукты", start, endExclusive);

            Assert.Equal(3, items.Count);
            Assert.Equal(new DateTime(2026, 9, 2), items[0].Date);   // сортировка: новые сверху
            Assert.Equal(new DateTime(2026, 8, 26), items[1].Date);
            Assert.Equal(new DateTime(2026, 8, 22), items[2].Date);
        }

        // --- Шаг 6: прогноз «хватит до даты» до конца периода ---

        [Fact]
        public void Forecast_ProjectPeriod_ToCustomPeriodEnd()
        {
            var (start, endExclusive) = Period.Resolve(AsOf);
            var f = ForecastEngine.ProjectPeriod(Fixture(), AsOf, start, endExclusive);

            // Горизонт = до конца периода (5 сентября), без остатка на следующий
            Assert.Equal(new DateTime(2026, 9, 5), f.HorizonEnd);
            Assert.False(f.RunsOut); // 69200 стартового хватает
            Assert.Equal("Хватит на весь срок", f.RunwayText);
        }

        // --- Шаг 7: еженедельный платёж проецируется внутри периода целиком ---

        [Fact]
        public void WeeklyRecurring_ProjectedAcrossPeriod()
        {
            var (start, endExclusive) = Period.Resolve(AsOf);
            // База 6.08 — до старта периода; следующие дни 13, 20, 27.08, 03.09
            var txs = new[] { Exp("Развлечения", 500, new DateTime(2026, 8, 6), Recurrence.Weekly) };

            var s = _service.SummarizeRange(txs, start, endExclusive);

            // В окно попадают 27.08 и 03.09 (13.08 и 20.08 — до старта, 06.08 — база)
            Assert.Equal(1000m, s.Expense);
        }

        // --- Шаг 8: повторяющийся платёж с датой окончания = дате старта не повторяется (UI-контракт BUG-1) ---

        [Fact]
        public void Recurring_WithEndDateAtStart_IsEffectivelyOneTime_InPeriod()
        {
            var (start, endExclusive) = Period.Resolve(AsOf);
            var baseDate = new DateTime(2026, 8, 21);

            // Такую запись раньше молча создавал пикер по умолчанию (= сегодня)
            var txs = new[] { Exp("Жилье", 30000, baseDate, Recurrence.Monthly, end: baseDate) };

            var s = _service.SummarizeRange(txs, start, endExclusive);

            // Только базовое вхождение — следующий месяц уже после конца
            Assert.Equal(30000m, s.Expense);
        }

        // --- Шаг 9: группа истории по дням, новые сверху ---

        [Fact]
        public void HistoryGroups_GroupedByDay_NewestFirst()
        {
            var groups = _service.GroupByDay(Fixture(), newestFirst: true);

            Assert.Equal(8, groups.Count); // 01, 21, 22, 23, 24, 26, 28.08 и 02.09
            for (int i = 1; i < groups.Count; i++)
                Assert.True(groups[i - 1].Items[0].Date > groups[i].Items[0].Date);
        }

        // --- Шаг 10: прогноз при уже отрицательном балансе (BUG-5) ---

        [Fact]
        public void Forecast_AlreadyNegativeBalance_RunoutIsToday()
        {
            var txs = new[] { Exp("Продукты", 30000, AsOf), Inc(100, AsOf.AddDays(-5)) };

            var f = ForecastEngine.Project(txs, AsOf);

            Assert.True(f.RunsOut);
            Assert.Equal(AsOf.Date, f.RunoutDate);
            Assert.Equal(-29900m, f.StartBalance);
            Assert.Contains("Денег хватит до", f.RunwayText);
        }

        // --- Шаг 11: рост дохода в сценарии применяется за (склонённый) горизонт ---

        [Fact]
        public void Scenario_IncomeRaise_AppliedPerDay()
        {
            var f = ForecastEngine.Project(Array.Empty<Transaction>(), AsOf, horizonDays: 45);
            var withRise = ScenarioEngine.Apply(Array.Empty<Transaction>(),
                new Scenario { Name = "Подработка", IncomeChange = 30000 }, AsOf, horizonDays: 45);

            // +1000/день × 45 дней горизонта
            Assert.Equal(45000m, withRise.HorizonEndBalance - f.HorizonEndBalance);
        }
    }
}