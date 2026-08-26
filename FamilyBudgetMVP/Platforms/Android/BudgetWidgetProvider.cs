using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Views;
using Android.Widget;
using SQLite;
using System.IO;

namespace FamilyBudgetMVP;

[BroadcastReceiver(Label = "Бюджет+", Exported = true)]
[IntentFilter(new[] { AppWidgetManager.ActionAppwidgetUpdate })]
[MetaData("android.appwidget.provider", Resource = "@xml/widget_info")]
public class BudgetWidgetProvider : AppWidgetProvider
{
    public override void OnUpdate(Context context, AppWidgetManager appWidgetManager, int[] appWidgetIds)
    {
        foreach (var widgetId in appWidgetIds)
        {
            UpdateWidget(context, appWidgetManager, widgetId);
        }
    }

    private static void UpdateWidget(Context context, AppWidgetManager appWidgetManager, int widgetId)
    {
        var views = new RemoteViews(context.PackageName, Resource.Layout.widget_balance);

        try
        {
            var dbPath = Path.Combine(context.FilesDir.Parent!, "files", "budget.db");

            if (File.Exists(dbPath))
            {
                var (balance, income, expense) = ReadBudget(dbPath);
                views.SetTextViewText(Resource.Id.widget_balance, $"{balance:N0} ₽");
                views.SetTextViewText(Resource.Id.widget_income, $"+{income:N0} ₽");
                views.SetTextViewText(Resource.Id.widget_expense, $"-{expense:N0} ₽");
            }
            else
            {
                views.SetTextViewText(Resource.Id.widget_balance, "Нет данных");
                views.SetTextViewText(Resource.Id.widget_income, "—");
                views.SetTextViewText(Resource.Id.widget_expense, "—");
            }
        }
        catch
        {
            views.SetTextViewText(Resource.Id.widget_balance, "Ошибка");
        }

        var now = DateTime.Now;
        views.SetTextViewText(Resource.Id.widget_updated, $"Обновлено: {now:HH:mm}");

        appWidgetManager.UpdateAppWidget(widgetId, views);
    }

    private static (decimal balance, decimal income, decimal expense) ReadBudget(string dbPath)
    {
        decimal income = 0, expense = 0;

        using var conn = new SQLiteConnection(dbPath);
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);

        var rows = conn.Query<TransactionRow>(
            "SELECT Amount FROM [Transaction] WHERE CreatedAt >= ? AND CreatedAt < ?",
            start, end);

        foreach (var row in rows)
        {
            if (row.Amount > 0)
                income += row.Amount;
            else
                expense += Math.Abs(row.Amount);
        }

        return (income - expense, income, expense);
    }

    private class TransactionRow
    {
        public decimal Amount { get; set; }
    }
}
