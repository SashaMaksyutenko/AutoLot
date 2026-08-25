using AutoLot.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AutoLot.Tests.TestDoubles;

/// <summary>
/// Тимчасова база для тестів. Це SQLite, який живе в пам'яті, а не PostgreSQL:
/// підняти його — мілісекунди, і кожен тест дістає власну чисту базу, тож
/// тести не заважають один одному й не залежать від порядку запуску.
///
/// Чого такий підхід НЕ перевіряє: усього, що є лише в PostgreSQL — блокувань
/// рядків, поведінки під одночасним доступом, особливостей типів. Для ставок
/// на аукціоні (SPEC §5) цього буде замало, там потрібна справжня база.
/// Для правил обраного вистачає: там звичайні вставки, вибірки й видалення.
///
/// IDisposable — щоб з'єднання закривалося саме тоді, коли тест закінчився:
/// база в пам'яті живе рівно доти, доки відкрите з'єднання до неї.
/// </summary>
internal sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection connection;

    public TestDatabase()
    {
        // ":memory:" каже SQLite тримати все в оперативній пам'яті.
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using var context = CreateContext();

        // EnsureCreated будує схему прямо з моделі EF, оминаючи міграції:
        // міграції написані під PostgreSQL і в SQLite не виконалися б.
        context.Database.EnsureCreated();
    }

    public AutoLotDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AutoLotDbContext>()
            .UseSqlite(connection)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new SqliteDbContext(options);
    }

    public void Dispose()
    {
        connection.Dispose();
    }

    /// <summary>
    /// Той самий контекст, але з однією поправкою на SQLite.
    ///
    /// У SQLite немає окремого типу для дати з часовим поясом — він зберігає
    /// її як текст і через це відмовляється сортувати за таким стовпцем.
    /// PostgreSQL це вміє, тож у робочому коді жодної проблеми немає, і
    /// підганяти його під обмеження тестового двійника було б неправильно.
    ///
    /// Перетворювач кладе значення в базу числом. Числа SQLite сортує
    /// прекрасно, а порядок у числах той самий, що й у датах, тож запит
    /// «найсвіжіше зверху» перевіряється чесно.
    /// </summary>
    private sealed class SqliteDbContext(DbContextOptions<AutoLotDbContext> options)
        : AutoLotDbContext(options)
    {
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);

            configurationBuilder.Properties<DateTimeOffset>()
                .HaveConversion<DateTimeOffsetToBinaryConverter>();
        }
    }
}
