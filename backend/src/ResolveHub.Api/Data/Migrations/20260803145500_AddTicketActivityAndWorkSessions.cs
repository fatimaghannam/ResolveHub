using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResolveHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketActivityAndWorkSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WorkDurationMinutes",
                table: "TicketHistory",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TicketWorkSession",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketID = table.Column<int>(type: "int", nullable: false),
                    ITAgentUserAccountID = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationMinutes = table.Column<int>(type: "int", nullable: true),
                    EndedReason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketWorkSession", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TicketWorkSession_Ticket_TicketID",
                        column: x => x.TicketID,
                        principalTable: "Ticket",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketWorkSession_UserAccount_ITAgentUserAccountID",
                        column: x => x.ITAgentUserAccountID,
                        principalTable: "UserAccount",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketWorkSession_ITAgentUserAccountID_StartedAt",
                table: "TicketWorkSession",
                columns: new[] { "ITAgentUserAccountID", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketWorkSession_TicketID",
                table: "TicketWorkSession",
                column: "TicketID",
                unique: true,
                filter: "[EndedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TicketWorkSession_TicketID_StartedAt",
                table: "TicketWorkSession",
                columns: new[] { "TicketID", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketWorkSession");

            migrationBuilder.DropColumn(
                name: "WorkDurationMinutes",
                table: "TicketHistory");
        }
    }
}
