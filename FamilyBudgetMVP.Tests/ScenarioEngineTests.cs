using FamilyBudgetMVP.Models;
using FamilyBudgetMVP.Services;

namespace FamilyBudgetMVP.Tests
{
    /// <summary>Сценарии «что если»: изменение дохода/расхода и разовая субсидия.</summary>
    public class ScenarioEngineTests
    {
        private static readonly DateTime AsOf = new(2026, 8, 24);

        [Fact]
        public void ExtraExpense_ShortensRunway()
        {
            // Баланс на сегодня: 50000 − 30000 = 20000, траты ~1000/день
            var txs = new List<Transaction>
            {
                new() { Description = "доход", Amount = 50000, Date = AsOf.AddDays(-40) },
                new() { Description = "траты", Amount = -30000, Date = AsOf.AddDays(-15) }
            };
            var @base = ForecastEngine.Project(txs, AsOf);

            // Сценарий: +15000 расходов в месяц (+500/день)
            var scenario = new Scenario { Name = "Ипотека", ExpenseChange = 15000 };
            var with = ScenarioEngine.Apply(txs, scenario, AsOf);

            Assert.True(@base.RunsOut);
            Assert.Equal(20000, @base.StartBalance);
            Assert.True(with.RunoutDate < @base.RunoutDate);
        }

        [Fact]
        public void OneTimeSubsidy_ShiftsRunway_ByFullAmount()
        {
            var txs = new List<Transaction> { new() { Description = "остаток", Amount = 5000, Date = AsOf } };

            var subsidy = new Scenario { Name = "Субсидия ЖКХ", OneTimeSubsidy = 5000 };
            (_, ForecastResult with) = ScenarioEngine.Compare(txs, subsidy, AsOf, horizonDays: 30);

            // Без трат баланс не уйдёт в минус ни так, ни так; проверяем сумму
            Assert.Equal(10000, with.HorizonEndBalance);
        }

        [Fact]
        public void IncomeIncrease_ImprovesEndBalance_Exactly()
        {
            var txs = new List<Transaction>();

            var scenario = new Scenario { Name = "Подработка", IncomeChange = 30000 };

            (ForecastResult @base, ForecastResult with) = ScenarioEngine.Compare(txs, scenario, AsOf, horizonDays: 30);

            // +30000/30 = +1000 в день × 30 дней горизонта
            Assert.Equal(with.HorizonEndBalance - @base.HorizonEndBalance, 30000m);
            Assert.False(@base.RunsOut);
        }

        [Fact]
        public void Compare_KeepsBaseIntact()
        {
            var txs = new List<Transaction> { new() { Description = "x", Amount = 1000, Date = AsOf } };
            var scenario = new Scenario { Name = "s", ExpenseChange = 9000 };

            (ForecastResult @base, _) = ScenarioEngine.Compare(txs, scenario, AsOf);

            Assert.Equal(1000, @base.StartBalance);
            Assert.Equal(1000, @base.HorizonEndBalance); // без сценария трат нет
        }
    }
}
