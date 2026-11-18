using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Energoreestr.Data.Migrations;

/// <summary>
/// Переписує файл бази начисто. Видалені рядки SQLite лишає у вільних сторінках,
/// а там могли лишитися записи про підприємців з попередніх імпортів.
/// VACUUM не працює всередині транзакції, тому він в окремій міграції.
/// </summary>
public partial class VacuumAfterSoleProprietors : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("VACUUM;", suppressTransaction: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
