using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResolveHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RestoreManagerAssignmentApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RequestedAgentUserAccountID",
                table: "TicketAssignmentRequest",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewReason",
                table: "TicketAssignmentRequest",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketAssignmentRequest_RequestedAgentUserAccountID",
                table: "TicketAssignmentRequest",
                column: "RequestedAgentUserAccountID");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketAssignmentRequest_UserAccount_RequestedAgentUserAccountID",
                table: "TicketAssignmentRequest",
                column: "RequestedAgentUserAccountID",
                principalTable: "UserAccount",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketAssignmentRequest_UserAccount_RequestedAgentUserAccountID",
                table: "TicketAssignmentRequest");

            migrationBuilder.DropIndex(
                name: "IX_TicketAssignmentRequest_RequestedAgentUserAccountID",
                table: "TicketAssignmentRequest");

            migrationBuilder.DropColumn(
                name: "RequestedAgentUserAccountID",
                table: "TicketAssignmentRequest");

            migrationBuilder.DropColumn(
                name: "ReviewReason",
                table: "TicketAssignmentRequest");
        }
    }
}
