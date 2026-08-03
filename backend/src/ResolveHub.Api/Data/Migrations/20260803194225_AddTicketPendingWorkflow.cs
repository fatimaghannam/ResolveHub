using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResolveHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketPendingWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketPendingRecord",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketID = table.Column<int>(type: "int", nullable: false),
                    WorkSessionID = table.Column<int>(type: "int", nullable: true),
                    ReasonCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReasonText = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    AdditionalNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserAccountID = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResumedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResumedByUserAccountID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketPendingRecord", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TicketPendingRecord_TicketWorkSession_WorkSessionID",
                        column: x => x.WorkSessionID,
                        principalTable: "TicketWorkSession",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketPendingRecord_Ticket_TicketID",
                        column: x => x.TicketID,
                        principalTable: "Ticket",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketPendingRecord_UserAccount_CreatedByUserAccountID",
                        column: x => x.CreatedByUserAccountID,
                        principalTable: "UserAccount",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketPendingRecord_UserAccount_ResumedByUserAccountID",
                        column: x => x.ResumedByUserAccountID,
                        principalTable: "UserAccount",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketPendingRecord_CreatedByUserAccountID",
                table: "TicketPendingRecord",
                column: "CreatedByUserAccountID");

            migrationBuilder.CreateIndex(
                name: "IX_TicketPendingRecord_ResumedByUserAccountID",
                table: "TicketPendingRecord",
                column: "ResumedByUserAccountID");

            migrationBuilder.CreateIndex(
                name: "IX_TicketPendingRecord_TicketID",
                table: "TicketPendingRecord",
                column: "TicketID",
                unique: true,
                filter: "[ResumedDate] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TicketPendingRecord_TicketID_CreatedDate",
                table: "TicketPendingRecord",
                columns: new[] { "TicketID", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketPendingRecord_WorkSessionID",
                table: "TicketPendingRecord",
                column: "WorkSessionID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketPendingRecord");
        }
    }
}
