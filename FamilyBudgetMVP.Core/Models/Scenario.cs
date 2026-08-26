using SQLite;

namespace FamilyBudgetMVP.Models
{
    /// <summary>
    /// Сценарий «что если»: как изменится прогноз, если скорректировать
    /// доход/расход или добавить разовую субсидию.
    /// </summary>
    [Table("scenarios")]
    public class Scenario
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        // Изменение месячного дохода (может быть отрицательным)
        public decimal IncomeChange { get; set; }

        // Изменение месячного расхода (положительное = тратим больше)
        public decimal ExpenseChange { get; set; }

        // Разовая субсидия/выплата
        public decimal OneTimeSubsidy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
