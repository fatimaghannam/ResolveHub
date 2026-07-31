using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResolveHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DuplicateReviewWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OriginalTicketID",
                table: "Ticket",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DuplicateReview",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketID = table.Column<int>(type: "int", nullable: false),
                    SuggestedOriginalTicketID = table.Column<int>(type: "int", nullable: false),
                    ReportedByUserAccountID = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReviewedByUserAccountID = table.Column<int>(type: "int", nullable: true),
                    ReviewedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuplicateReview", x => x.ID);
                    table.ForeignKey(
                        name: "FK_DuplicateReview_Ticket_SuggestedOriginalTicketID",
                        column: x => x.SuggestedOriginalTicketID,
                        principalTable: "Ticket",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DuplicateReview_Ticket_TicketID",
                        column: x => x.TicketID,
                        principalTable: "Ticket",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DuplicateReview_UserAccount_ReportedByUserAccountID",
                        column: x => x.ReportedByUserAccountID,
                        principalTable: "UserAccount",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DuplicateReview_UserAccount_ReviewedByUserAccountID",
                        column: x => x.ReviewedByUserAccountID,
                        principalTable: "UserAccount",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserNotification",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserAccountID = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    TicketReferenceNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotification", x => x.ID);
                    table.ForeignKey(
                        name: "FK_UserNotification_UserAccount_UserAccountID",
                        column: x => x.UserAccountID,
                        principalTable: "UserAccount",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_OriginalTicketID",
                table: "Ticket",
                column: "OriginalTicketID");

            migrationBuilder.CreateIndex(
                name: "IX_DuplicateReview_ReportedByUserAccountID",
                table: "DuplicateReview",
                column: "ReportedByUserAccountID");

            migrationBuilder.CreateIndex(
                name: "IX_DuplicateReview_ReviewedByUserAccountID",
                table: "DuplicateReview",
                column: "ReviewedByUserAccountID");

            migrationBuilder.CreateIndex(
                name: "IX_DuplicateReview_Status_CreatedDate",
                table: "DuplicateReview",
                columns: new[] { "Status", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DuplicateReview_SuggestedOriginalTicketID",
                table: "DuplicateReview",
                column: "SuggestedOriginalTicketID");

            migrationBuilder.CreateIndex(
                name: "IX_DuplicateReview_TicketID_Status",
                table: "DuplicateReview",
                columns: new[] { "TicketID", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotification_UserAccountID_IsRead_CreatedDate",
                table: "UserNotification",
                columns: new[] { "UserAccountID", "IsRead", "CreatedDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_Ticket_Ticket_OriginalTicketID",
                table: "Ticket",
                column: "OriginalTicketID",
                principalTable: "Ticket",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ticket_Ticket_OriginalTicketID",
                table: "Ticket");

            migrationBuilder.DropTable(
                name: "DuplicateReview");

            migrationBuilder.DropTable(
                name: "UserNotification");

            migrationBuilder.DropIndex(
                name: "IX_Ticket_OriginalTicketID",
                table: "Ticket");

            migrationBuilder.DropColumn(
                name: "OriginalTicketID",
                table: "Ticket");

        }
    }
}
