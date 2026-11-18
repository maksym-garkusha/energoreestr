using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Energoreestr.Data.Migrations;

/// <summary>
/// Записи про фізичних осіб-підприємців більше не зберігаються, лишається тільки їхня
/// кількість у файлі. У базі, зібраній попередньою версією, вони ще є: тут їх видаляємо
/// разом із журналом змін і значеннями в зауваженнях. Файл бази стискає наступна
/// міграція, <see cref="VacuumAfterSoleProprietors"/>.
/// </summary>
public partial class CountSoleProprietorsOnly : Migration
{
    // останній файл кожного набору: саме його стан лежить у таблицях
    private const string IsLatest = """
        NOT EXISTS (SELECT 1 FROM Snapshots later
                    WHERE later.Dataset = Snapshots.Dataset
                      AND (later.PublishedAt > Snapshots.PublishedAt
                           OR (later.PublishedAt = Snapshots.PublishedAt AND later.Id > Snapshots.Id)))
        """;

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "SoleProprietors",
            table: "Snapshots",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.Sql($"""
            UPDATE Snapshots
            SET SoleProprietors = CASE Dataset
                    WHEN 'wholesale' THEN (SELECT COUNT(*) FROM Participants WHERE IsIndividual = 1)
                    ELSE (SELECT COUNT(*) FROM Monopolies m WHERE m.Dataset = Snapshots.Dataset AND m.IsIndividual = 1)
                END
            WHERE {IsLatest};

            UPDATE Snapshots SET ImportedRows = ImportedRows - SoleProprietors;
            """);

        // У журналі підприємця видно за ключем, поки він ще є в таблиці. Тих, хто вже
        // зник з реєстру, впізнаємо за позначкою в назві, а в монополіях за ключем без
        // коду, якого немає серед організацій
        migrationBuilder.Sql("""
            DELETE FROM Changes
            WHERE Field = 'IsIndividual'
               OR (Dataset = 'wholesale' AND Key IN (SELECT EcrbCode FROM Participants WHERE IsIndividual = 1))
               OR (Dataset <> 'wholesale' AND Key IN (SELECT Key FROM Monopolies WHERE IsIndividual = 1))
               OR Name LIKE '%ФІЗИЧНА ОСОБА%' OR Name LIKE '%Фізична особа%' OR Name LIKE 'ФОП %'
               OR (Dataset <> 'wholesale' AND Key NOT GLOB '[0-9]*'
                   AND Key NOT IN (SELECT Key FROM Monopolies WHERE IsIndividual = 0));
            """);

        migrationBuilder.Sql("""
            UPDATE Issues SET Key = 'фізична особа-підприємець', Value = NULL
            WHERE (Dataset = 'wholesale' AND Key IN (SELECT EcrbCode FROM Participants WHERE IsIndividual = 1))
               OR (Dataset <> 'wholesale' AND Key IN (SELECT Name FROM Monopolies WHERE IsIndividual = 1));
            """);

        // Десять цифр, як у РНОКПП, попередня версія записувала в зауваження до організації
        // і ключем, і значенням. До якої саме організації воно належить, тут не встановити,
        // тому зауваження видаляємо. Наступний імпорт запише його під назвою і без коду
        migrationBuilder.Sql($"""
            DELETE FROM Issues WHERE {TenDigits("Key")} OR {TenDigits("Value")};

            UPDATE Snapshots
            SET IssueCount = (SELECT COUNT(*) FROM Issues i WHERE i.Dataset = Snapshots.Dataset)
            WHERE {IsLatest};
            """);

        migrationBuilder.Sql("""
            DELETE FROM Participants WHERE IsIndividual = 1;
            DELETE FROM Monopolies WHERE IsIndividual = 1;
            """);

        // EF для DropColumn перебудовує таблицю окремими транзакціями з PRAGMA foreign_keys,
        // а SQLite з версії 3.35 сам знімає колонку, на якій немає індексів
        migrationBuilder.Sql("""
            ALTER TABLE Participants DROP COLUMN IsIndividual;
            ALTER TABLE Monopolies DROP COLUMN IsIndividual;
            """);
    }

    /// <inheritdoc />
    /// <remarks>Повертає тільки схему: видалені записи відновить лише новий імпорт старою версією.</remarks>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE Snapshots DROP COLUMN SoleProprietors;
            ALTER TABLE Participants ADD COLUMN IsIndividual INTEGER NOT NULL DEFAULT 0;
            ALTER TABLE Monopolies ADD COLUMN IsIndividual INTEGER NOT NULL DEFAULT 0;
            """);
    }

    // код набирали руками, тож у ньому бувають пробіли, дефіси й крапка в кінці
    private static string TenDigits(string column) =>
        $"replace(replace(replace({column}, ' ', ''), '-', ''), '.', '') GLOB '{string.Concat(Enumerable.Repeat("[0-9]", 10))}'";
}
