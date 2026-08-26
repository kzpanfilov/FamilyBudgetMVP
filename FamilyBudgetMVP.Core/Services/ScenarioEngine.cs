using FamilyBudgetMVP.Models;

namespace FamilyBudgetMVP.Services
{
    /// <summary>
    /// Сценарии «что если»: пересчёт прогноза при изменении дохода/расхода
    /// или добавлении разовой субсидии, сравнение с базовым прогнозом.
    /// Семантика изменений:
    ///   IncomeChange  — прибавка к месячному доходу, размазывается по дням;
    ///   ExpenseChange — прибавка к месячному расходу, аналогично по дням;
    ///   OneTimeSubsidy— разовая выплата на следующий день после asOf.
    /// </summary>
    public static class ScenarioEngine
    {
        public static ForecastResult Apply(IEnumerable<Transaction> transactions, Scenario scenario, DateTime asOf, int horizonDays = ForecastEngine.DefaultHorizonDays)
        {
            decimal incomePerDay = scenario.IncomeChange / 30m;
            decimal expensePerDay = scenario.ExpenseChange / 30m;

            return ForecastEngine.ProjectCore(
                transactions, asOf, horizonDays,
                extraDailyDelta: _ => incomePerDay - expensePerDay,
                oneTimeOnNextDay: scenario.OneTimeSubsidy != 0 ? scenario.OneTimeSubsidy : null);
        }

        /// <summary>Базовый и сценарный прогнозы рядом.</summary>
        public static (ForecastResult Base, ForecastResult WithScenario) Compare(
            IEnumerable<Transaction> transactions, Scenario scenario, DateTime asOf, int horizonDays = ForecastEngine.DefaultHorizonDays)
        {
            var @base = ForecastEngine.Project(transactions, asOf, horizonDays);
            var with = Apply(transactions, scenario, asOf, horizonDays);
            return (@base, with);
        }
    }
}
