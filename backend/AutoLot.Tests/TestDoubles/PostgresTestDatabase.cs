using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace AutoLot.Tests.TestDoubles;

/// <summary>
/// Тимчасове місце для даних у СПРАВЖНЬОМУ PostgreSQL.
///
/// Навіщо, коли є швидкий SQLite у <see cref="TestDatabase"/>: блокування
/// рядка (SELECT … FOR UPDATE) — це поведінка конкретної СУБД. SQLite її не
/// має взагалі, тож перевірити на ньому головний ризик проєкту (SPEC §5)
/// неможливо: тест зеленів би, нічого не доводячи.
///
/// Створюється не окрема база, а окрема СХЕМА в тій самій. Схема — це ніби
/// тека всередині бази: таблиці в різних схемах не бачать одна одної, тож
/// робочі дані лишаються недоторканими. Причина саме такого вибору проста:
/// створення бази вимагає окремого права CREATEDB, якого в робочої ролі
/// немає й не повинно бути, а створення схеми — звичайна операція.
///
/// Потрібен запущений PostgreSQL — той самий, на якому працює застосунок.
/// </summary>
internal sealed class PostgresTestDatabase : IAsyncDisposable
{
    private readonly string schemaName;

    private PostgresTestDatabase(string schemaName, string connectionString)
    {
        this.schemaName = schemaName;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public static async Task<PostgresTestDatabase> CreateAsync()
    {
        var baseConnectionString = ResolveConnectionString();

        // Випадкова назва: два запуски тестів поруч не заважатимуть один одному.
        var schemaName = $"test_{Guid.NewGuid():N}";

        await ExecuteAsync(baseConnectionString, $"CREATE SCHEMA \"{schemaName}\"");

        // Search Path каже PostgreSQL, у якій схемі шукати таблиці без явної
        // назви схеми. Завдяки цьому і запити EF, і наш власний SQL із
        // FOR UPDATE потрапляють саме в тимчасову схему — без жодної правки
        // робочого коду.
        var connectionString = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            SearchPath = schemaName,
        }.ConnectionString;

        var database = new PostgresTestDatabase(schemaName, connectionString);

        await using (var context = database.CreateContext())
        {
            // EnsureCreated тут не годиться: він вважає, що база вже створена,
            // якщо в ній є бодай одна таблиця, — а робочі таблиці нікуди не
            // ділися. Тому беремо в EF готовий скрипт створення схеми й
            // виконуємо його самі; Search Path приведе його куди треба.
            var script = context.Database.GenerateCreateScript();

            await ExecuteAsync(connectionString, script);
        }

        return database;
    }

    public AutoLotDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AutoLotDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AutoLotDbContext(options);
    }

    public async ValueTask DisposeAsync()
    {
        // CASCADE прибирає разом зі схемою все, що в ній лежить, — таблиці,
        // індекси, зв'язки. Робочої схеми public це не стосується.
        await ExecuteAsync(ConnectionString, $"DROP SCHEMA IF EXISTS \"{schemaName}\" CASCADE");

        // З'єднання в пулі пам'ятають Search Path на схему, якої вже немає.
        NpgsqlConnection.ClearAllPools();
    }

    /// <summary>
    /// Той самий рядок підключення, яким користується застосунок: спершу
    /// user-secrets проєкту AutoLot.Api, потім змінні оточення.
    /// </summary>
    private static string ResolveConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(Program).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        return DatabaseConnection.Resolve(configuration);
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
