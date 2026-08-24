using SQLite;

namespace FamilyBudgetMVP.Models
{
    public class Transaction
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Description { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;

        public string Category { get; set; } = "Разное";

        // Вспомогательное свойство для красивого вывода в UI
        public string FormattedAmount => (Amount >= 0 ? "+" : "") + Amount.ToString("N2") + " ₽";

        public string DateShort => Date.ToString("dd.MM.yyyy");

        // Время операции: день уже указан в заголовке группы истории
        public string TimeText => Date.ToString("HH:mm");
    }
}
