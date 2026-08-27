using System.Security.Cryptography;
using System.Text;

namespace FamilyBudgetMVP.Services
{
    public enum PremiumActivationStatus
    {
        Valid,
        Invalid,
        Expired
    }

    public readonly record struct PremiumActivationResult(PremiumActivationStatus Status, DateTime? ValidUntilUtc)
    {
        public bool IsValid => Status == PremiumActivationStatus.Valid;
    }

    /// <summary>
    /// Код активации премиума: подпись HMAC-SHA256 по дате окончания,
    /// кодированная в base32-Crockford без двусмысленных символов.
    /// Код — bearer-билет «премиум до даты»; проверяется в приложении офлайн.
    /// </summary>
    public static class PremiumActivation
    {
        // Секрет совпадает с ключом, которым бот подписывает коды при выдаче.
        private const string Secret = "BudgetPlus~P8w2!vQc9#mR4&tY7@sK3^eF5$nB1";

        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"; // без I,L,O,U
        private const string Prefix = "BP-";

        private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static string Generate(DateTime validUntilUtc)
        {
            var payload = EncodePayload(validUntilUtc);
            var signature = Sign(payload);
            var code = Base32Encode(Concat(payload, signature));
            return Prefix + Group(code);
        }

        public static PremiumActivationResult Validate(string? code, DateTime nowUtc)
        {
            if (string.IsNullOrWhiteSpace(code))
                return new PremiumActivationResult(PremiumActivationStatus.Invalid, null);

            var normalized = Normalize(code);
            if (normalized is null)
                return new PremiumActivationResult(PremiumActivationStatus.Invalid, null);

            var decoded = Base32Decode(normalized);
            if (decoded is null || decoded.Length != 12)
                return new PremiumActivationResult(PremiumActivationStatus.Invalid, null);

            var payload = decoded.AsSpan(0, 4);
            var signature = decoded.AsSpan(4, 8);

            if (!CryptographicOperations.FixedTimeEquals(signature, Sign(payload)))
                return new PremiumActivationResult(PremiumActivationStatus.Invalid, null);

            var validUntil = DecodePayload(payload);
            if (validUntil <= nowUtc)
                return new PremiumActivationResult(PremiumActivationStatus.Expired, validUntil);

            return new PremiumActivationResult(PremiumActivationStatus.Valid, validUntil);
        }

        private static byte[] EncodePayload(DateTime validUntilUtc)
        {
            long unix = (long)(validUntilUtc.ToUniversalTime() - Epoch).TotalSeconds;
            if (unix < 0 || unix > uint.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(validUntilUtc), "Дата вне диапазона кода.");

            return BitConverter.GetBytes((uint)unix);
        }

        private static DateTime DecodePayload(ReadOnlySpan<byte> payload)
        {
            uint unix = BitConverter.ToUInt32(payload);
            return Epoch.AddSeconds(unix);
        }

        private static byte[] Sign(ReadOnlySpan<byte> payload)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
            return hmac.ComputeHash(payload.ToArray())[..8];
        }

        private static byte[] Concat(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        {
            var result = new byte[a.Length + b.Length];
            a.CopyTo(result);
            b.CopyTo(result.AsSpan(a.Length));
            return result;
        }

        private static string? Normalize(string code)
        {
            var sb = new StringBuilder(code.Length);
            foreach (var ch in code.Trim().ToUpperInvariant())
            {
                if (ch is '-' or ' ')
                    continue;
                sb.Append(ch);
            }

            var text = sb.ToString();
            if (text.StartsWith(Prefix.Replace("-", ""), StringComparison.Ordinal))
                text = text[(Prefix.Length - 1)..];

            return text.Length == 20 ? text : null;
        }

        private static string Group(string code) =>
            code.Length == 20
                ? string.Join('-', code[..5], code.Substring(5, 5), code.Substring(10, 5), code.Substring(15, 5))
                : code;

        private static string Base32Encode(ReadOnlySpan<byte> data)
        {
            var sb = new StringBuilder();
            int buffer = 0;
            int bits = 0;

            foreach (var b in data)
            {
                buffer = (buffer << 8) | b;
                bits += 8;
                while (bits >= 5)
                {
                    sb.Append(Alphabet[(buffer >> (bits - 5)) & 0x1F]);
                    bits -= 5;
                }
            }

            if (bits > 0)
                sb.Append(Alphabet[(buffer << (5 - bits)) & 0x1F]);

            return sb.ToString();
        }

        private static byte[]? Base32Decode(string text)
        {
            int buffer = 0;
            int bits = 0;
            var result = new List<byte>();

            foreach (var ch in text)
            {
                int value = Alphabet.IndexOf(ch);
                if (value < 0)
                    return null;

                buffer = (buffer << 5) | value;
                bits += 5;
                if (bits >= 8)
                {
                    result.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                    bits -= 8;
                }
            }

            return result.ToArray();
        }
    }
}