namespace FamilyBudgetMVP.Services
{
    /// <summary>
    /// Зона попадания на графике: прямоугольник + привязанная категория.
    /// Используется для определения, по какому столбцу ткнул пользователь.
    /// </summary>
    public record HitZone(float Left, float Top, float Right, float Bottom, string Category)
    {
        public bool Contains(float x, float y) =>
            x >= Left && x <= Right && y >= Top && y <= Bottom;
    }

    /// <summary>
    /// Детектор попадания по зонам графика.
    /// Без зависимостей от SkiaSharp — можно тестировать из Core.
    /// </summary>
    public class ChartHitDetector
    {
        private readonly List<HitZone> _zones = new();

        public void AddZone(float left, float top, float right, float bottom, string category)
        {
            _zones.Add(new HitZone(left, top, right, bottom, category));
        }

        public void Clear() => _zones.Clear();

        public int Count => _zones.Count;

        /// <summary>
        /// Категория по координатам или null, если попадание не в одну зону.
        /// </summary>
        public string? HitTest(float x, float y)
        {
            foreach (var zone in _zones)
            {
                if (zone.Contains(x, y))
                    return zone.Category;
            }

            return null;
        }
    }
}
