using FamilyBudgetMVP.Models;

namespace FamilyBudgetMVP.Models
{
    /// <summary>Строка плоского списка истории: заголовок дня либо операция.</summary>
    public abstract class HistoryRow
    {
    }

    /// <summary>Заголовок дня («Сегодня», «d MMMM») с итогом дня.</summary>
    public class HistoryDayHeader : HistoryRow
    {
        public string Title { get; init; } = string.Empty;
        public string DayTotalText { get; init; } = string.Empty;
    }

    /// <summary>Строка операции. Проксирует поля для биндингов шаблона.</summary>
    public class HistoryTransactionRow : HistoryRow
    {
        public required Transaction Transaction { get; init; }

        public string Description => Transaction.Description;
        public string Category => Transaction.Category;
        public string TimeText => Transaction.TimeText;
        public string DateShort => Transaction.DateShort;
        public string FormattedAmount => Transaction.FormattedAmount;
        public decimal Amount => Transaction.Amount;

        public static implicit operator Transaction(HistoryTransactionRow row) => row.Transaction;
    }
}
