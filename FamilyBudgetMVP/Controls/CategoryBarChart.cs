using Microcharts;
using SkiaSharp;

namespace FamilyBudgetMVP.Controls
{
    /// <summary>
    /// BarChart с собственной легендой (встроенная не отрисовывается в WinUI)
    /// и полными значениями над столбцами.
    ///
    /// Ключевые знания о библиотеке (Microcharts 2.0.0.3 + SkiaSharp 4.x):
    /// - без IsAnimated=false AnimationProgress остается 0, и весь текст
    ///   рисуется прозрачным;
    /// - AxisBasedChart строит слоты оси только по entries первой серии,
    ///   поэтому GenerateDefaultSerie создает по серии на категорию: в каждой
    ///   все N позиций, реальное значение только на своей (остальные -
    ///   заглушки ChartEntry(null), библиотека их пропускает);
    /// - DrawLine в SkiaSharp 4.x не рисует без StrokeWidth > 0.
    /// </summary>
    public class CategoryBarChart : BarChart
    {
        private const float LegendLineGap = 4f;
        private const int MaxLegendLines = 2;

        // Слоты столбцов, заполненные при последней отрисовке (для хит-теста кликов)
        private readonly List<(SKRect Rect, string Label)> _barSlots = new();

        /// <summary>
        /// Категория по координатам канвы (пиксели) либо null.
        /// Слот шире самого столбца — попадание засчитывается по всей колонке.
        /// </summary>
        public string? HitTest(float x, float y)
        {
            foreach (var slot in _barSlots)
            {
                if (slot.Rect.Contains(x, y))
                    return slot.Label;
            }

            return null;
        }

        public override void DrawContent(SKCanvas canvas, int width, int height)
        {
            _barSlots.Clear();
            base.DrawContent(canvas, width, height);
        }

        protected override void GenerateDefaultSerie(IEnumerable<ChartEntry> value)
        {
            var entries = value.ToList();

            var series = entries.Select((entry, index) => new ChartSerie
            {
                Name = entry.Label,
                Color = entry.Color,
                Entries = entries.Select((e, i) => i == index
                    ? e
                    : new ChartEntry(null) { Label = e.Label }).ToList()
            }).ToList();

            UpdateSeries(series);
        }

        // Резервируем высоту под собственную легенду над областью графика
        protected override float CalculateHeaderHeight(Dictionary<ChartEntry, SKRect> valueLabelSizes)
        {
            return base.CalculateHeaderHeight(valueLabelSizes) + MaxLegendLines * (SerieLabelTextSize + LegendLineGap) + Margin;
        }

        // Рисуем легенду сами: цветной квадрат + название категории + доля в процентах
        protected override void OnDrawContentEnd(SKCanvas canvas, SKSize itemSize, float origin, Dictionary<ChartEntry, SKRect> valueLabelSizes)
        {
            if (Series == null)
                return;

            float width = canvas.DeviceClipBounds.Width;
            float textSize = SerieLabelTextSize;
            float x = Margin;
            float y = Margin;

            // Доли считаем от суммы значений всех серий
            float total = Series.SelectMany(s => s.Entries)
                .Where(e => e.Value.HasValue)
                .Sum(e => e.Value!.Value);

            using var font = new SKFont();
            font.Size = textSize;

            using var textPaint = new SKPaint
            {
                IsAntialias = true,
                Color = LabelColor
            };

            foreach (var serie in Series)
            {
                if (string.IsNullOrEmpty(serie.Name))
                    continue;

                float value = serie.Entries.FirstOrDefault(e => e.Value.HasValue)?.Value ?? 0;
                float percent = total > 0 ? value / total * 100f : 0;
                string text = $"{serie.Name} · {percent:0}%";

                font.MeasureText(text, out var textBounds);
                float itemWidth = textSize + 6 + textBounds.Width + Margin;

                if (x + itemWidth > width && x > Margin)
                {
                    x = Margin;
                    y += textSize + LegendLineGap;
                }

                using (var fill = new SKPaint { Style = SKPaintStyle.Fill, Color = serie.Color ?? SKColors.Gray })
                {
                    canvas.DrawRect(SKRect.Create(x, y, textSize, textSize), fill);
                }

                canvas.DrawText(text, x + textSize + 6, y + textSize - 3, SKTextAlign.Left, font, textPaint);
                x += itemWidth;
            }
        }

        // Значение над столбцом без обрезки по ширине столбца
        // (библиотека урезает текст до 3/1 символа, если он шире столбца).
        // Заодно запоминаем слот столбца для хит-теста кликов.
        protected override void DrawValueLabel(SKCanvas canvas, Dictionary<ChartEntry, SKRect> valueLabelSizes, float headerWithLegendHeight, SKSize itemSize, SKSize barSize, ChartEntry entry, float barX, float barY, float itemX, float origin)
        {
            if (entry is null || string.IsNullOrEmpty(entry.ValueLabel))
                return;

            using var font = new SKFont();
            font.Size = ValueLabelTextSize;

            using var paint = new SKPaint
            {
                IsAntialias = true,
                Color = entry.ValueLabelColor.WithAlpha((byte)(255 * AnimationProgress))
            };

            float centerX = barX - itemSize.Width / 2 + barSize.Width / 2;
            float baseline = barY - Margin;

            canvas.DrawText(entry.ValueLabel, centerX, baseline, SKTextAlign.Center, font, paint);

            // Слот на всю ширину колонки и высоту области графика — удобная зона клика
            float slotLeft = centerX - itemSize.Width / 2;
            _barSlots.Add((SKRect.Create(slotLeft, 0, itemSize.Width, Math.Max(origin, barY) + Margin), entry.Label));
        }
    }
}
