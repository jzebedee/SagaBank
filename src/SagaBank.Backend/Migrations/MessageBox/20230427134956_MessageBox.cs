using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SagaBank.Backend.Migrations.MessageBox
{
    /// <inheritdoc />
    public partial class MessageBox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Inbox",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BLOB", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Added = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inbox", x => new { x.Id, x.Type });
                });

            migrationBuilder.CreateTable(
                name: "Outbox",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BLOB", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Added = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    Payload = table.Column<byte[]>(type: "BLOB", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outbox", x => new { x.Id, x.Type });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Inbox");

            migrationBuilder.DropTable(
                name: "Outbox");
        }
    }
}
