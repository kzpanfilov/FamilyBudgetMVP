namespace FamilyBudgetMVP.Services
{
    /// <summary>Функционал для freemium-модели.</summary>
    public enum Feature
    {
        /// <summary>Базовый учёт — всегда бесплатный.</summary>
        Tracking,

        /// <summary>Прогноз остатка — бесплатно по ТЗ MVP.</summary>
        Forecast,

        /// <summary>Сценарии «что если» — premium.</summary>
        Scenarios,

        /// <summary>Шаблоны документов — premium.</summary>
        Templates,

        /// <summary>Полный справочник льгот — premium (базовый регион бесплатный).</summary>
        FullBenefits
    }

    /// <summary>
    /// Флаги монетизации: переключение одной константой.
    /// UI проверяет доступ через IsUnlocked перед показом/запуском функции.
    /// </summary>
    public static class FeatureGate
    {
        // TODO(monetization): купить/активировать премиум здесь
        public static bool IsPremium { get; } = false;

        public static bool IsUnlocked(Feature feature) => feature switch
        {
            Feature.Tracking or Feature.Forecast => true,
            Feature.Scenarios or Feature.Templates or Feature.FullBenefits => IsPremium,
            _ => true
        };
    }
}
