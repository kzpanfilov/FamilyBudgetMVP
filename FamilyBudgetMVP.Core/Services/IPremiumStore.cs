namespace FamilyBudgetMVP.Services
{
    /// <summary>
    /// Персистентное хранилище статуса премиума. Реализация в MAUI-проекте
    /// поверх Preferences (Core не зависит от платформенных API).
    /// </summary>
    public interface IPremiumStore
    {
        bool IsPremium { get; }

        /// <summary>Срок действия премиума (UTC) или null, если премиума нет.</summary>
        DateTime? ValidUntilUtc { get; }

        void Activate(DateTime validUntilUtc);

        void Deactivate();
    }
}