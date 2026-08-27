using FamilyBudgetMVP.Services;

namespace FamilyBudgetMVP.Tests
{
    public class PremiumActivationTests
    {
        private static readonly DateTime Present = new(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Generate_Produces_Grouped_Code_With_Prefix()
        {
            var code = PremiumActivation.Generate(Present.AddYears(1));

            Assert.StartsWith("BP-", code);
            Assert.Equal(5 + 1 + 5 + 1 + 5 + 1 + 5 + 3, code.Length); // BP- + 4x5 через '-'
        }

        [Fact]
        public void Validate_Accepts_Own_Code()
        {
            var until = Present.AddYears(1);
            var code = PremiumActivation.Generate(until);

            var result = PremiumActivation.Validate(code, Present);

            Assert.True(result.IsValid);
            Assert.Equal(until, result.ValidUntilUtc!.Value, precision: TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void Validate_Accepts_Lowercase_And_Dashes_Stripped()
        {
            var until = Present.AddYears(1);
            var code = PremiumActivation.Generate(until);

            var result = PremiumActivation.Validate(code.ToLowerInvariant().Replace("-", ""), Present);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_Rejects_Tampered_Code()
        {
            var code = PremiumActivation.Generate(Present.AddYears(1));
            char[] chars = code.ToCharArray();
            int i = Array.IndexOf(chars, '-') + 1; // первый символ данных после префикса
            chars[i] = chars[i] == '0' ? '1' : '0';

            var result = PremiumActivation.Validate(new string(chars), Present);

            Assert.Equal(PremiumActivationStatus.Invalid, result.Status);
        }

        [Fact]
        public void Validate_Rejects_Empty_And_Null()
        {
            Assert.Equal(PremiumActivationStatus.Invalid, PremiumActivation.Validate("", Present).Status);
            Assert.Equal(PremiumActivationStatus.Invalid, PremiumActivation.Validate(null, Present).Status);
        }

        [Fact]
        public void Validate_Rejects_Garbage()
        {
            Assert.Equal(PremiumActivationStatus.Invalid, PremiumActivation.Validate("BP-HELLO-WORLD-HELLO-WORLD", Present).Status);
        }

        [Fact]
        public void Validate_Rejects_Expired_Code()
        {
            var code = PremiumActivation.Generate(Present.AddMonths(-1));

            var result = PremiumActivation.Validate(code, Present);

            Assert.Equal(PremiumActivationStatus.Expired, result.Status);
        }

        [Fact]
        public void Codes_Are_Stable_For_Same_Date()
        {
            var until = Present.AddYears(1);

            Assert.Equal(PremiumActivation.Generate(until), PremiumActivation.Generate(until));
        }
    }

    public class FeatureGateTests
    {
        private sealed class FakeStore(bool premium, DateTime? until) : IPremiumStore
        {
            public bool IsPremium { get; } = premium;
            public DateTime? ValidUntilUtc { get; } = until;
            public void Activate(DateTime validUntilUtc) { }
            public void Deactivate() { }
        }

        [Fact]
        public void Free_Features_Unlocked_Without_Premium()
        {
            var store = new FakeStore(false, null);
            FeatureGate.PremiumStore = store;

            Assert.True(FeatureGate.IsUnlocked(Feature.Tracking));
            Assert.True(FeatureGate.IsUnlocked(Feature.Forecast));
        }

        [Fact]
        public void Premium_Features_Locked_Without_Premium()
        {
            var store = new FakeStore(false, null);
            FeatureGate.PremiumStore = store;

            Assert.False(FeatureGate.IsUnlocked(Feature.Scenarios));
            Assert.False(FeatureGate.IsUnlocked(Feature.Templates));
            Assert.False(FeatureGate.IsUnlocked(Feature.FullBenefits));
        }

        [Fact]
        public void Premium_Features_Unlocked_With_Premium()
        {
            var store = new FakeStore(true, DateTime.UtcNow.AddYears(1));
            FeatureGate.PremiumStore = store;

            Assert.True(FeatureGate.IsUnlocked(Feature.Scenarios));
            Assert.True(FeatureGate.IsUnlocked(Feature.Templates));
            Assert.True(FeatureGate.IsUnlocked(Feature.FullBenefits));
        }

        [Fact]
        public void IsPremium_Reflects_Store()
        {
            FeatureGate.PremiumStore = new FakeStore(false, null);
            Assert.False(FeatureGate.IsPremium);

            FeatureGate.PremiumStore = new FakeStore(true, DateTime.UtcNow.AddDays(30));
            Assert.True(FeatureGate.IsPremium);
        }
    }
}