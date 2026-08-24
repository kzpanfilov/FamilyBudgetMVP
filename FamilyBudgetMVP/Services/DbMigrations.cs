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
        public const int CurrentVersion = 1;

        private static readonly object Gate = new();

        public static void Apply(SQLiteAsyncConnection db)
        {
            // Инициализация может вызываться из двух коннектов (TransactionService,
            // CategoryStore) — сериализуем, чтобы шаги не выполнялись параллельно
            lock (Gate)
            {
                int version = db.ExecuteScalarAsync<int>("PRAGMA user_version").Result;

                // v1: базовые таблицы операций и категорий (историческая схема)
                if (version < 1)
                {
                    db.CreateTableAsync<Transaction>().Wait();
                    db.CreateTableAsync<Category>().Wait();
                }

                // Пример будущей миграции:
                // if (version < 2)
                //     db.ExecuteAsync("ALTER TABLE transactions ADD COLUMN Currency TEXT NOT NULL DEFAULT 'RUB'").Wait();

                if (version < CurrentVersion)
                    db.ExecuteAsync($"PRAGMA user_version = {CurrentVersion}").Wait();
            }
        }
    }
}
