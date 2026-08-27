using System.Collections.ObjectModel;
using FamilyBudgetMVP.Controls;
using FamilyBudgetMVP.Helpers;
using FamilyBudgetMVP.Models;
using FamilyBudgetMVP.Services;
using FamilyBudgetMVP.Views;

using Microcharts;
using Microcharts.Maui;
using SkiaSharp;
using SkiaSharp.Views.Maui.Controls; // Microcharts ���������� ��� ���������� ��� ���������

namespace FamilyBudgetMVP;

/// <summary>
/// �������: ������, ������ �������� � ��������� ��������.
/// ������ �������� � � BudgetService, �������� � � TransactionService,
/// ��������� � � CategoryStore. ����� ������ UI-�������.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly TransactionService _txService;
    private readonly BudgetService _budgetService;
    private readonly CategoryStore _categories;

    private ObservableCollection<Transaction> _transactions = new();

    public MainPage(TransactionService txService, BudgetService budgetService, CategoryStore categories)
    {
        InitializeComponent();
        _txService = txService;
        _budgetService = budgetService;
        _categories = categories;

        // График и история обновляются при изменении категорий
        _categories.Changed += RefreshAll;

        if (Application.Current != null)
            Application.Current.RequestedThemeChanged += OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => UpdateCharts();

    // Тексты осей/значений графика: на тёмном фоне нужен светлый вариант
    private static bool IsDarkTheme =>
        Application.Current?.RequestedTheme == AppTheme.Dark;

    private static SKColor AxisTextColor =>
        SKColor.Parse(IsDarkTheme ? "#93A3A8" : "#5B6B70");

    private static Color MutedTextColor =>
        Color.FromArgb(IsDarkTheme ? "#93A3A8" : "#5B6B70");

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // ��������� ������ ����� �������� ��������� ����� ����������
        LoadDataAsync();
    }

    private async void OnOpenAddTransaction(object? sender, EventArgs e)
    {
        await Navigation.PushModalAsync(ServiceHelper.Get<AddTransactionPage>());
    }

    private async void LoadDataAsync()
    {
        try
        {
            // Гарантируем готовность БД и кэша категорий (идемпотентно)
            await _txService.InitializeAsync();
            await _categories.InitializeAsync();

            var data = await _txService.GetTransactionsAsync();

            _transactions.Clear();
            foreach (var t in data)
            {
                _transactions.Add(t);
            }

            RefreshAll();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Загрузка данных");
            BalanceLabel.Text = "Ошибка БД";
        }
    }

    // ������ ����� ���������� ����� ��������� ������
    private void RefreshAll()
    {
        // Changed может прийти из фонового прогрева сервисов (Task.Run в MauiProgram) —
        // мутации UI-коллекций обязаны идти через диспетчер
        if (!Dispatcher.IsDispatchRequired)
        {
            UpdateBalance();
            RebuildHistory();
            UpdateCharts();
            return;
        }

        Dispatcher.Dispatch(() =>
        {
            UpdateBalance();
            RebuildHistory();
            UpdateCharts();
        });
    }

    private void RebuildHistory()
    {
        // Плоский список строк (заголовок дня + операции) вместо IsGrouped:
        // группировочный обработчик WinUI нестабилен, а плоский список
        // заодно позволяет скроллить и выбирать строки без сюрпризов
        var rows = new List<HistoryRow>();

        foreach (var group in _budgetService.GroupByDay(_transactions))
        {
            rows.Add(new HistoryDayHeader
            {
                Title = group.Title,
                DayTotalText = group.DayTotalText
            });

            foreach (var t in group.Items)
                rows.Add(new HistoryTransactionRow { Transaction = t });
        }

        // BindableLayout на StackLayout: без ItemsView-хендлера WinUI,
        // страница прокручивается целиком
        BindableLayout.SetItemsSource(TransactionsList, rows);
    }

    private void UpdateCharts()
    {
        var categories = _categories.All;
        var (start, endExclusive) = ServiceHelper.Get<BudgetPeriodStore>().GetCurrent().Resolve(DateTime.Today);

        // ������ ������: ������ �������� ������, �������� ���������� � �������
        var entries = _budgetService.BuildRangeExpenseEntries(
            _transactions, start, endExclusive, categories,
            defaultValueLabelHex: IsDarkTheme ? "#E8EDEC" : "#1F2A2E");
        UpdateLimitWarnings(categories, start, endExclusive);

        if (entries.Count == 0)
        {
            ChartView.Content = new Label
            {
                Text = "📊 Расходов за этот месяц нет",
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                FontFamily = "OpenSansSemibold",
                TextColor = MutedTextColor
            };
        }
        else
        {
            var barChart = new CategoryBarChart()
            {
                IsAnimated = false, // ����� AnimationProgress=0 � ����� ����������
                Entries = entries,
                LabelOrientation = Orientation.Horizontal,
                ValueLabelOrientation = Orientation.Horizontal,
                Margin = 8,
                MaxValue = entries.Max(e => Math.Abs(e.Value ?? 0)),
                LabelColor = AxisTextColor,
                ValueLabelOption = ValueLabelOption.TopOfElement,
                SerieLabelTextSize = 16,
                LegendOption = SeriesLegendOption.None
            };

            var innerChartView = CreateTappableChart(barChart);

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += OnChartTapped;
            innerChartView.GestureRecognizers.Add(tapGesture);

            ChartView.Content = innerChartView;
        }

        // �������� ������ �������� �� ����
        var dailyEntries = _budgetService.BuildDailyExpenseEntries(_transactions);

        if (dailyEntries.Any(e => (e.Value ?? 0) > 0))
        {
            DynamicsView.Content = CreateTappableChart(new LineChart
            {
                IsAnimated = false,
                Entries = dailyEntries,
                Margin = 8,
                LineSize = 3,
                LineAreaAlpha = 20,
                PointMode = PointMode.None,
                LabelTextSize = 11,
                LabelColor = AxisTextColor
            });
        }
        else
        {
            DynamicsView.Content = new Label
            {
                Text = "📊 Расходов за последние 30 дней нет",
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                FontFamily = "OpenSansSemibold",
                TextColor = MutedTextColor
            };
        }
    }

    private ChartView CreateTappableChart(Chart chart)
    {
        return new ChartView
        {
            Chart = chart,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
    }

    private void OnChartTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            if (sender is not View container ||
                container.Width <= 0 || container.Height <= 0)
                return;

            var innerChart = container switch
            {
                ContentView cv => cv.Content as ChartView,
                ChartView cv => cv,
                _ => null
            };

            if (innerChart is not ChartView chartView ||
                chartView.Chart is not CategoryBarChart barChart ||
                chartView.CanvasSize.Width <= 0)
                return;

            var position = e.GetPosition(container);
            if (position is null)
                return;

            float scale = (float)(chartView.CanvasSize.Width / container.Width);
            string? category = barChart.HitTest((float)(position.Value.X * scale), (float)(position.Value.Y * scale));

            if (category != null)
                Dispatcher.DispatchAsync(() => OpenCategoryDetail(category));
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "OnChartTapped");
        }
    }

    private async Task OpenCategoryDetail(string category)
    {
        try
        {
            var (start, endExclusive) = ServiceHelper.Get<BudgetPeriodStore>().GetCurrent().Resolve(DateTime.Today);
            var periodItems = _budgetService.FilterByCategoryRange(_transactions, category, start, endExclusive);

            await Navigation.PushModalAsync(new CategoryDetailPage(category, periodItems, start, endExclusive));
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "OpenCategoryDetail");
        }
    }

    private void UpdateLimitWarnings(IReadOnlyList<Category> categories, DateTime start, DateTime endExclusive)
    {
        var issues = _budgetService.CheckLimitsInRange(_transactions, categories, start, endExclusive);

        if (issues.Count == 0)
        {
            LimitWarningCard.IsVisible = false;
            return;
        }

        LimitWarningCard.IsVisible = true;
        LimitWarningLabel.Text = "⚠  " + string.Join("  ·  ", issues.Select(s =>
            s.Exceeded
                ? $"{s.Category}: {s.Spent:N0} из {s.Limit:N0} ₽ — превышение на {s.Spent - s.Limit:N0} ₽"
                : $"{s.Category}: {s.Spent:N0} из {s.Limit:N0} ₽ — в пределах лимита"));
    }

    private void UpdateBalance()
    {
        var now = DateTime.Today;
        var period = ServiceHelper.Get<BudgetPeriodStore>().GetCurrent();
        var (start, endExclusive) = period.Resolve(now);

        var s = _budgetService.SummarizeRange(_transactions, start, endExclusive);

        BalanceLabel.Text = $"{s.Balance:N2} ₽";
        IncomeLabel.Text = $"↑  {s.Income:N0} ₽";
        ExpenseLabel.Text = $"↓  {s.Expense:N0} ₽";

        PeriodLabel.Text = $"Период: {period.FormatRange(now)}";

        // Прогноз «до какой даты хватит денег» (ТЗ MVP), горизонт — конец периода
        var forecast = ForecastEngine.ProjectPeriod(_transactions, DateTime.Today, start, endExclusive);
        RunwayLabel.Text = forecast.RunwayText;
        RunwayChip.IsVisible = true;
    }

    private static Transaction? GetRowTransaction(object? sender)
    {
        return (sender as BindableObject)?.BindingContext switch
        {
            HistoryTransactionRow row => row.Transaction,
            Transaction tx => tx,
            _ => null
        };
    }

    private async void OnDeleteTransactionClicked(object? sender, EventArgs e)
    {
        if (GetRowTransaction(sender) is { } transaction)
            await TryDeleteTransactionAsync(transaction);
    }

    private async void OnDeleteTransactionTapped(object? sender, TappedEventArgs e)
    {
        if (GetRowTransaction(sender) is { } transaction)
            await TryDeleteTransactionAsync(transaction);
    }

    private async Task TryDeleteTransactionAsync(Transaction transaction)
    {
        // ���������� ������������� (������� ��� UX)
        bool confirm = await DisplayAlertAsync("Подтверждение", $"Удалить запись: {transaction.Description}?", "Да", "Нет");

        if (!confirm) return;

        try
        {
            await _txService.DeleteTransactionAsync(transaction.Id);

            _transactions.Remove(transaction);
            RefreshAll();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Удаление операции");
            await DisplayAlertAsync("Ошибка", $"Не удалось удалить: {ex.Message}", "OK");
        }
    }

    private void OnRowPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Grid row)
            foreach (var child in row.Children.OfType<Button>())
                child.IsVisible = true;
    }

    private void OnRowPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Grid row)
            foreach (var child in row.Children.OfType<Button>())
                child.IsVisible = false;
    }

    // Тап по строке истории — редактирование операции
    private async void OnRowTapped(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is HistoryTransactionRow { Transaction: { } tx })
            await OpenEditAsync(tx);
    }

    private async Task OpenEditAsync(Transaction tx)
    {
        var page = ServiceHelper.Get<AddTransactionPage>();
        page.SetupForEdit(tx);
        await Navigation.PushModalAsync(page);
    }
}
