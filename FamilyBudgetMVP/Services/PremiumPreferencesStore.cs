namespace FamilyBudgetMVP.Services;

public sealed class PremiumPreferencesStore : IPremiumStore
{
    private const string ValidUntilKey = "premium.valid_until";
    private const string ActivatedKey = "premium.activated";

    public bool IsPremium
    {
        get
        {
            if (!Preferences.Get(ActivatedKey, false))
                return false;

            var until = ValidUntilUtc;
            return until == null || until.Value.ToUniversalTime() > DateTime.UtcNow;
        }
    }

    public DateTime? ValidUntilUtc
    {
        get
        {
            if (!Preferences.Get(ActivatedKey, false))
                return null;

            long ticks = Preferences.Get(ValidUntilKey, 0L);
            return ticks > 0 ? new DateTime(ticks, DateTimeKind.Utc) : null;
        }
    }

    public void Activate(DateTime validUntilUtc)
    {
        Preferences.Set(ValidUntilKey, validUntilUtc.ToUniversalTime().Ticks);
        Preferences.Set(ActivatedKey, true);
    }

    public void Deactivate()
    {
        Preferences.Remove(ValidUntilKey);
        Preferences.Remove(ActivatedKey);
    }
}