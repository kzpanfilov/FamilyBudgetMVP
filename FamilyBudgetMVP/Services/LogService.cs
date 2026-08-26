using System.Text;

namespace FamilyBudgetMVP.Services
{
    /// <summary>
    /// Простой файловый лог (AppData/logs/app-YYYY-MM.log).
    /// Логирование никогда не должно ломать приложение — все сбои глотаются.
    /// </summary>
    public static class LogService
    {
        private static readonly object Gate = new();

        /// <summary>UTF-8 with BOM — чтобы логи корректно открывались в любом просмотрщике.</summary>
        private static readonly Encoding Utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

        public static void Error(Exception ex, string context)
            => Write("ERROR", $"{context}: {ex}");

        public static void Info(string message)
            => Write("INFO", message);

        private static void Write(string level, string message)
        {
            try
            {
                string dir = Path.Combine(FileSystem.AppDataDirectory, "logs");
                Directory.CreateDirectory(dir);

                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";

                lock (Gate)
                {
                    File.AppendAllText(
                        Path.Combine(dir, $"app-{DateTime.Now:yyyy-MM}.log"),
                        line,
                        Utf8Bom);
                }
            }
            catch
            {
                // лог недоступен — не падаем
            }
        }
    }
}
