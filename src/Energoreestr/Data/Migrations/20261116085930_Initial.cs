using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Energoreestr.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DatasetStates",
                columns: table => new
                {
                    Dataset = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    UpdateFrequency = table.Column<string>(type: "TEXT", nullable: true),
                    ResourceName = table.Column<string>(type: "TEXT", nullable: true),
                    ResourceModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CheckedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatasetStates", x => x.Dataset);
                });

            migrationBuilder.CreateTable(
                name: "Issues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Dataset = table.Column<string>(type: "TEXT", nullable: false),
                    Line = table.Column<int>(type: "INTEGER", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Issues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Monopolies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Dataset = table.Column<string>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Edrpou = table.Column<string>(type: "TEXT", nullable: true),
                    IsIndividual = table.Column<bool>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    SearchName = table.Column<string>(type: "TEXT", nullable: false),
                    Location = table.Column<string>(type: "TEXT", nullable: false),
                    Activity = table.Column<string>(type: "TEXT", nullable: false),
                    Territory = table.Column<string>(type: "TEXT", nullable: false),
                    FirstSnapshotId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Monopolies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Participants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EcrbCode = table.Column<string>(type: "TEXT", nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    SearchName = table.Column<string>(type: "TEXT", nullable: false),
                    LegalForm = table.Column<string>(type: "TEXT", nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: true),
                    Settlement = table.Column<string>(type: "TEXT", nullable: true),
                    Zip = table.Column<string>(type: "TEXT", nullable: true),
                    Country = table.Column<string>(type: "TEXT", nullable: false),
                    Eic = table.Column<string>(type: "TEXT", nullable: true),
                    Bic = table.Column<string>(type: "TEXT", nullable: true),
                    Lei = table.Column<string>(type: "TEXT", nullable: true),
                    Vat = table.Column<string>(type: "TEXT", nullable: true),
                    IdCode = table.Column<string>(type: "TEXT", nullable: true),
                    IsIndividual = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubmittedOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    RegisteredOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ExcludedOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Website = table.Column<string>(type: "TEXT", nullable: true),
                    InsideInfoUrl = table.Column<string>(type: "TEXT", nullable: true),
                    FirstSnapshotId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Participants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Snapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Dataset = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    TotalRows = table.Column<int>(type: "INTEGER", nullable: false),
                    ImportedRows = table.Column<int>(type: "INTEGER", nullable: false),
                    IssueCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Added = table.Column<int>(type: "INTEGER", nullable: false),
                    Removed = table.Column<int>(type: "INTEGER", nullable: false),
                    Changed = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Changes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SnapshotId = table.Column<int>(type: "INTEGER", nullable: false),
                    Dataset = table.Column<string>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    Field = table.Column<string>(type: "TEXT", nullable: true),
                    OldValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Changes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Changes_Snapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "Snapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Changes_Key",
                table: "Changes",
                column: "Key");

            migrationBuilder.CreateIndex(
                name: "IX_Changes_SnapshotId",
                table: "Changes",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_Dataset_Code",
                table: "Issues",
                columns: new[] { "Dataset", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_Monopolies_Dataset_Key",
                table: "Monopolies",
                columns: new[] { "Dataset", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Monopolies_Edrpou",
                table: "Monopolies",
                column: "Edrpou");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_EcrbCode",
                table: "Participants",
                column: "EcrbCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Participants_Eic",
                table: "Participants",
                column: "Eic");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_IdCode",
                table: "Participants",
                column: "IdCode");

            migrationBuilder.CreateIndex(
                name: "IX_Snapshots_Dataset_PublishedAt",
                table: "Snapshots",
                columns: new[] { "Dataset", "PublishedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Changes");

            migrationBuilder.DropTable(
                name: "DatasetStates");

            migrationBuilder.DropTable(
                name: "Issues");

            migrationBuilder.DropTable(
                name: "Monopolies");

            migrationBuilder.DropTable(
                name: "Participants");

            migrationBuilder.DropTable(
                name: "Snapshots");
        }
    }
}
