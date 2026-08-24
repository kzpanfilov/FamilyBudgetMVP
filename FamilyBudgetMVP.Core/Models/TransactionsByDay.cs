namespace FamilyBudgetMVP.Models
{
    /// <summary>Группа истории операций за один день.</summary>
    public class TransactionsByDay
    {
        public string Title { get; init; } = string.Empty;
        public List<Transaction> Items { get; init; } = new();

        private decimal Total => Items.Sum(t => t.Amount);

        public string DayTotalText => (Total >= 0 ? "+" : "−") + Math.Abs(Total).ToString("N0") + " ₽";
    }
}
