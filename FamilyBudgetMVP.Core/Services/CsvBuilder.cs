using System.Globalization;
using System.Text;
using FamilyBudgetMVP.Models;

namespace FamilyBudgetMVP.Services
{
    /// <summary>
    /// Формирование CSV. Разделитель «;» и десятичная запятая — так файл
    /// корректно открывается русским Excel без импорт-мастера.
    /// </summary>
    public static class CsvBuilder
    {
        public static string BuildTransactionsCsv(IEnumerable<Transaction> transactions)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Дата;Тип;Категория;Сумма;Описание");

            foreach (var t in transactions.OrderBy(x => x.Date))
            {
                sb.Append(Escape(t.Date.ToString("dd.MM.yyyy HH:mm"))).Append(';');
                sb.Append(Escape(t.Amount >= 0 ? "Доход" : "Расход")).Append(';');
                sb.Append(Escape(t.Category)).Append(';');
                sb.Append(Escape(t.Amount.ToString("N2", CultureInfo.GetCultureInfo("ru-RU")))).Append(';');
                sb.AppendLine(Escape(t.Description));
            }

            return sb.ToString();
        }

        // Кавычки ставим только там, где они нужны: разделитель, кавычка или перенос строки
        internal static string Escape(string? value)
        {
            value ??= string.Empty;

            bool mustQuote = value.Contains(';') || value.Contains('"') ||
                             value.Contains('\n') || value.Contains('\r');

            if (!mustQuote)
                return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
