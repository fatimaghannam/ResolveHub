using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResolveHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketCancellationRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketCancellationRequest",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketID = table.Column<int>(type: "int", nullable: false),
                    RequestedByAgentUserAccountID = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedByManagerUserAccountID = table.Column<int>(type: "int", nullable: true),
                    ReviewedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketCancellationRequest", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TicketCancellationRequest_Ticket_TicketID",
                        column: x => x.TicketID,
                        principalTable: "Ticket",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketCancellationRequest_UserAccount_RequestedByAgentUserAccountID",
                        column: x => x.RequestedByAgentUserAccountID,
                        principalTable: "UserAccount",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketCancellationRequest_UserAccount_ReviewedByManagerUserAccountID",
                        column: x => x.ReviewedByManagerUserAccountID,
                        principalTable: "UserAccount",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketCancellationRequest_RequestedByAgentUserAccountID",
                table: "TicketCancellationRequest",
                column: "RequestedByAgentUserAccountID");

            migrationBuilder.CreateIndex(
                name: "IX_TicketCancellationRequest_ReviewedByManagerUserAccountID",
                table: "TicketCancellationRequest",
                column: "ReviewedByManagerUserAccountID");

            migrationBuilder.CreateIndex(
                name: "IX_TicketCancellationRequest_TicketID",
                table: "TicketCancellationRequest",
                column: "TicketID",
                unique: true,
                filter: "[Status] = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_TicketCancellationRequest_TicketID_Status",
                table: "TicketCancellationRequest",
                columns: new[] { "TicketID", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketCancellationRequest");
        }
    }
}
