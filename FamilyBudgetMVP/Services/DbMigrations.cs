using FamilyBudgetMVP.Models;
using SQLite;

namespace FamilyBudgetMVP.Services
{
    /// <summary>
    /// Миграции схемы БД. Версия хранится в PRAGMA user_version.
    ///
    /// Как добавлять миграцию:
    /// 1. Увеличить CurrentVersion.
    /// 2. Добавить блок: if (version &lt; N) { ...ALTER TABLE / CreateTable... }
    /// 3. Раннер сам доведёт старую базу пользователя до актуальной версии
    ///    последовательным применением всех недостающих шагов.
    /// </summary>
    public static class DbMigrations
    {
        public const int CurrentVersion = 3;

        private static readonly SemaphoreSlim _gateSlim = new(1, 1);

        /// <summary>
        /// Применяет недостающие миграции. Безопасно вызывать многократно.
        /// Только async: синхронное ожидание на UI-потоке даёт дедлок.
        /// </summary>
        public static async Task ApplyAsync(SQLiteAsyncConnection db)
        {
            await _gateSlim.WaitAsync();
            try
            {
                int version = await db.ExecuteScalarAsync<int>("PRAGMA user_version");

                // v1: базовые таблицы операций и категорий (историческая схема)
                if (version < 1)
                {
                    await db.CreateTableAsync<Transaction>();
                    await db.CreateTableAsync<Category>();
                }

                // v2: периодичность операций, источник, сценарии «что если»
                if (version < 2)
                {
                    // CreateTable в v1 уже создаёт свежую базу с новыми колонками,
                    // поэтому ALTER только при их отсутствии
                    await EnsureColumnAsync(db, "Transaction", "Source", "TEXT NOT NULL DEFAULT ''");
                    await EnsureColumnAsync(db, "Transaction", "RecurrenceType", "TEXT NOT NULL DEFAULT 'none'");
                    await EnsureColumnAsync(db, "Transaction", "RecurEndDate", "TEXT NULL");
                    await db.CreateTableAsync<Scenario>();
                }

                // v3: справочник льгот и субсидий
                if (version < 3)
                {
                    await db.CreateTableAsync<Benefit>();
                }

                if (version < CurrentVersion)
                    await db.ExecuteAsync($"PRAGMA user_version = {CurrentVersion}");
            }
            finally
            {
                _gateSlim.Release();
            }
        }

        private sealed class PragmaRow
        {
            public int cid { get; set; }
            public string name { get; set; } = string.Empty;
            public string type { get; set; } = string.Empty;
        }

        private static async Task EnsureColumnAsync(SQLiteAsyncConnection db, string table, string column, string definition)
        {
            // Transaction — зарезервированное слово; квотируем через [brackets].
            // SELECT + pragma_table_info() вместо PRAGMA, потому что QueryAsync<T>
            // оборачивает PRAGMA в SELECT … FROM, что ломает синтаксис.
            var sql = $"SELECT * FROM pragma_table_info('{table}')";
            var columns = await db.QueryAsync<PragmaRow>(sql);
            if (!columns.Any(c => string.Equals(c.name, column, StringComparison.OrdinalIgnoreCase)))
                await db.ExecuteAsync($"ALTER TABLE [{table}] ADD COLUMN {column} {definition}");
        }
    }
}
