using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResolveHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketAssignmentRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketAssignmentRequest",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketID = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserAccountID = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedByUserAccountID = table.Column<int>(type: "int", nullable: true),
                    ReviewedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketAssignmentRequest", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TicketAssignmentRequest_Ticket_TicketID",
                        column: x => x.TicketID,
                        principalTable: "Ticket",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketAssignmentRequest_UserAccount_RequestedByUserAccountID",
                        column: x => x.RequestedByUserAccountID,
                        principalTable: "UserAccount",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketAssignmentRequest_UserAccount_ReviewedByUserAccountID",
                        column: x => x.ReviewedByUserAccountID,
                        principalTable: "UserAccount",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketAssignmentRequest_RequestedByUserAccountID",
                table: "TicketAssignmentRequest",
                column: "RequestedByUserAccountID");

            migrationBuilder.CreateIndex(
                name: "IX_TicketAssignmentRequest_ReviewedByUserAccountID",
                table: "TicketAssignmentRequest",
                column: "ReviewedByUserAccountID");

            migrationBuilder.CreateIndex(
                name: "IX_TicketAssignmentRequest_Status_RequestedDate",
                table: "TicketAssignmentRequest",
                columns: new[] { "Status", "RequestedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketAssignmentRequest_TicketID_RequestedByUserAccountID_Status",
                table: "TicketAssignmentRequest",
                columns: new[] { "TicketID", "RequestedByUserAccountID", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketAssignmentRequest");
        }
    }
}
