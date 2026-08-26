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

        // Источник дохода (зарплата, пособие...) или место расхода; для истории = Description
        public string Source { get; set; } = string.Empty;

        // Периодичность: none | weekly | monthly | quarterly
        public string RecurrenceType { get; set; } = Recurrence.None;

        // До какой даты действует повторяющийся платёж (null — бессрочно)
        public DateTime? RecurEndDate { get; set; }

        public bool IsRecurring => RecurrenceType != Recurrence.None;

        public bool IsIncome => Amount > 0;

        // Вспомогательное свойство для красивого вывода в UI
        public string FormattedAmount => (Amount >= 0 ? "+" : "") + Amount.ToString("N2") + " ₽";

        public string DateShort => Date.ToString("dd.MM.yyyy");

        // Время операции: день уже указан в заголовке группы истории
        public string TimeText => Date.ToString("HH:mm");
    }
}
