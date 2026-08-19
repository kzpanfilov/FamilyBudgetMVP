using System.Collections.ObjectModel;
using FamilyBudgetMVP.Models;
using FamilyBudgetMVP.Services;
using System.Globalization;

using Microcharts;
using Microcharts.Maui;
using SkiaSharp;
using SkiaSharp.Views.Maui.Controls; // Microcharts использует эту библиотеку для отрисовки

namespace FamilyBudgetMVP;

public partial class MainPage : ContentPage
{
    private readonly DatabaseService _dbService;
    private ObservableCollection<Transaction> _transactions = new();

    public MainPage(DatabaseService dbService)
    {
        InitializeComponent();
        _dbService = dbService;
        TransactionsList.ItemsSource = _transactions;
        
        // Загружаем данные при старте
        LoadDataAsync();
    }

    private async void LoadDataAsync()
    {
        try 
        {
            var data = await _dbService.GetTransactionsAsync();
        
            _transactions.Clear();
            foreach (var t in data)
            {
                _transactions.Add(t);
            }

            UpdateBalance();
            // Обновляем график после загрузки данных
            UpdateChart(); 
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка загрузки: {ex.Message}");
            BalanceLabel.Text = "Ошибка БД";
        }
    }

    private void UpdateChart()
    {
        var entries = GenerateChartEntries();

        // 1. Создаем сам график (логика данных)
        // var chart = new BarChart
        // {
        //     Entries = entries,
        //     LabelOrientation = Orientation.Horizontal,
        //     ValueLabelOrientation = Orientation.Horizontal,
        //     BackgroundColor = SKColors.Transparent,
        //     //Padding = new Thickness(10),
        //     MaxValue = (float)(entries.Any() ? entries.Max(e => e.Value) * 1.2 : 100)
        // };

        // 2. Создаем холст (UI элемент), который умеет рисовать графики Microcharts
        /*var chartView = new ChartView
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };*/

        var lineChart = new LineChart()
        {
            Entries = entries
        };

        var maxValue = entries.Max(e => Math.Abs(e.Value.Value));
        
        var barChart = new MyBarChart()
        {
            Entries = entries,
            LabelOrientation = Orientation.Horizontal,
            ValueLabelOrientation = Orientation.Horizontal,
            //BackgroundColor = SKColors.Transparent,
            MaxValue = maxValue,
            //MinValue = 0,
            ShowYAxisLines = true,
            ShowYAxisText = true,
            YAxisLinesPaint = new SKPaint() { Color = SKColors.Blue, IsAntialias = true },
            //LabelTextSize = 20,
            ValueLabelOption = ValueLabelOption.TopOfElement,
            SerieLabelTextSize = 20,
            
            LegendOption = SeriesLegendOption.Top
        };
        
        //string format = "#,0,,.#0M"; // million, etc.
        //barChart.Series.ToList().ForEach(series => series.);        
        var chartView = new ChartView
        {
            //Frame = new Rect(),
           // AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable,
            Chart = barChart,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        
        
        

        // 3. Подписываемся на событие рисования: когда холст готов, скажи графику "нарисуйся здесь"
        /*canvasView.PaintSurface += (sender, args) =>
        {
            var surface = args.Surface;
            var skCanvas = surface.Canvas;
        
            // Очищаем холст
            skCanvas.Clear(SKColors.Transparent);

            // Рисуем наш график на этом холсте
            var skRect = args.Info.Rect;

            var left = skRect.Left;
            var top = skRect.Top;
            var right = skRect.Right;
            var  bottom = skRect.Bottom;

            var canvasRect = new SKRect(left, top, right, bottom);
            var width = canvasRect.Width;
            var  height = canvasRect.Height;

            chart.Draw(skCanvas, (int) width, (int) height);
        };*/

        // 4. Кладем холст в наш ContentView
        ChartView.Content =  chartView;
    }


    private void UpdateBalance()
    {
        decimal balance = _transactions.Sum(t => t.Amount);
        BalanceLabel.Text = $"{balance:F2} ₽";
        BalanceLabel.TextColor = balance >= 0 ? Colors.Green : Colors.Red;
    }

    private List<ChartEntry> GenerateChartEntries()
    {
        var entries = new List<ChartEntry>();

        // Группируем транзакции по категориям и суммируем Amount
        var grouped = _transactions
            .Where(t => t.Amount < 0) // Берем только расходы (отрицательные суммы)
            .GroupBy(t => t.Category)
            .Select(g => new { Category = g.Key, Total = g.Sum(t => t.Amount) })
            .OrderByDescending(x => x.Total) // Сортируем от самых больших трат
            .ToList();

        foreach (var group in grouped)
        {
            // Для графика нам нужно положительное число (высота столбца)
            float value = Convert.ToSingle(Math.Abs(group.Total)); 
        
            var entry = new ChartEntry(value)
            {
                Label = group.Category,
                ValueLabel = value.ToString("N0"), // Например: "3 500"
                ValueLabelColor = GetCategoryColor(group.Category),
                Color = GetCategoryColor(group.Category), // Цвет столбца
            };

            entries.Add(entry);
        }

        return entries;
    }

// Простой метод для подбора цвета (чтобы не было одинаковых)
    private SKColor GetCategoryColor(string category)
    {
        return category switch
        {
            "Продукты" => SKColors.Orange,   // Оранжевый
            "Транспорт" => SKColors.Brown, // Синий
            "Жилье" => SKColors.Violet,     // Фиолетовый
            "Развлечения" => SKColors.Yellow,// Желтый
            "Здоровье" => SKColors.Green,   // Зеленый
            _ => SKColors.Gray            // Серый для "Разное"
        };
    }
    
    // Обработчик кнопки "Добавить"
    private async void OnAddTransactionClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(DescriptionEntry.Text) || string.IsNullOrWhiteSpace(AmountEntry.Text))
        {
            await DisplayAlert("Ошибка", "Заполните описание и сумму!", "OK");
            return;
        }

        if (!decimal.TryParse(AmountEntry.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal amount))
        {
            await DisplayAlert("Ошибка", "Введите корректное число!", "OK");
            return;
        }

        try 
        {
            var newTransaction = new Transaction
            {
                Description = DescriptionEntry.Text,
                Amount = amount,
                // ✅ Берем выбранную категорию из Picker
                Category = CategoryPicker.SelectedItem?.ToString() ?? "Разное", 
                Date = DateTime.Now
            };

            await _dbService.SaveTransactionAsync(newTransaction);

            _transactions.Add(newTransaction);
            UpdateBalance();
            UpdateChart();

            DescriptionEntry.Text = string.Empty;
            AmountEntry.Text = string.Empty;
            CategoryPicker.SelectedIndex = -1; // Сброс выбора
        
            DescriptionEntry.Unfocus();
            AmountEntry.Unfocus();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка БД", $"Не удалось сохранить: {ex.Message}", "OK");
        }
    }

    private async void OnDeleteTransactionClicked(object sender, EventArgs e)
    {
        // Получаем кнопку, которая была нажата
        var button = sender as Button;
    
        // Получаем данные транзакции из BindingContext кнопки
        var transaction = button?.BindingContext as Transaction;

        if (transaction == null) return;

        // Спрашиваем подтверждение (хороший тон UX)
        bool confirm = await DisplayAlert("Подтверждение", $"Удалить запись: {transaction.Description}?", "Да", "Нет");

        if (!confirm) return;

        try 
        {
            await _dbService.DeleteTransactionAsync(transaction.Id);
        
            // Удаляем из коллекции (UI обновится автоматически)
            _transactions.Remove(transaction);
        
            UpdateBalance();
            UpdateChart();
        
            await DisplayAlert("Успех", "Запись удалена", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", $"Не удалось удалить: {ex.Message}", "OK");
        }
    }


    public class MyBarChart : BarChart
    {
        protected override void GenerateDefaultSerie(IEnumerable<ChartEntry> value)
        {
            UpdateSeries(value.Select(e => new ChartSerie { Entries = [e], Name = e.Label, Color = e.Color }));
        }
    }

}