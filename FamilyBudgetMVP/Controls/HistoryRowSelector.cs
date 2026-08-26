using FamilyBudgetMVP.Models;
using Microsoft.Maui.Controls;

namespace FamilyBudgetMVP.Controls
{
    /// <summary>
    /// Выбирает шаблон строки истории: заголовок дня или операция.
    /// Используется вместо IsGrouped у CollectionView — группировочный
    /// обработчик WinUI (GroupableItemsView) нестабилен (NRE в
    /// ItemTemplateContextEnumerable при смене источника).
    /// </summary>
    public class HistoryRowSelector : DataTemplateSelector
    {
        public DataTemplate? HeaderTemplate { get; set; }
        public DataTemplate? ItemTemplate { get; set; }

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
            => item is HistoryDayHeader ? HeaderTemplate! : ItemTemplate!;
    }
}
