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
    /// Флаги монетизации. Статус премиума читается из хранилища
    /// (<see cref="PremiumStore"/>), которое MAUI привязывает к Preferences
    /// при старте. UI проверяет доступ через IsUnlocked перед показом/запуском функции.
    /// </summary>
    public static class FeatureGate
    {
        // По умолчанию — без премиума, пока MAUI не подставит реальное хранилище.
        private static IPremiumStore _store = new NoPremiumStore();

        /// <summary>Источник статуса премиума. Задаётся один раз при старте приложения.</summary>
        public static IPremiumStore PremiumStore
        {
            get => _store;
            set => _store = value ?? throw new ArgumentNullException(nameof(value));
        }

        public static bool IsPremium => _store.IsPremium;

        /// <summary>Срок действия премиума (UTC) или null, если премиума нет.</summary>
        public static DateTime? ValidUntilUtc => _store.ValidUntilUtc;

        public static bool IsUnlocked(Feature feature) => feature switch
        {
            Feature.Tracking or Feature.Forecast => true,
            Feature.Scenarios or Feature.Templates or Feature.FullBenefits => IsPremium,
            _ => true
        };
    }

    /// <summary>Заглушка: никогда не премиум (до привязки реального хранилища).</summary>
    internal sealed class NoPremiumStore : IPremiumStore
    {
        public bool IsPremium => false;
        public DateTime? ValidUntilUtc => null;
        public void Activate(DateTime validUntilUtc) { }
        public void Deactivate() { }
    }
}