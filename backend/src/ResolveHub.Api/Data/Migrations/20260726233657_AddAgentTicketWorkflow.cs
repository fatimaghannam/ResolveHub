using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResolveHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentTicketWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE [Ticket]
                SET [TicketReferenceNumber] = CONCAT(
                    'RH-',
                    DATEPART(YEAR, [CreatedDate]),
                    '-',
                    CASE
                        WHEN LEN(CONVERT(varchar(20), [ID])) < 4
                            THEN RIGHT('0000' + CONVERT(varchar(20), [ID]), 4)
                        ELSE CONVERT(varchar(20), [ID])
                    END);
                """);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionSummary",
                table: "Ticket",
                type: "nvarchar(max)",
                maxLength: 5000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResolvedByUserAccountID",
                table: "Ticket",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Ticket",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "TicketComment",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketID = table.Column<int>(type: "int", nullable: false),
                    AuthorUserAccountID = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: false),
                    IsInternal = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsEdited = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketComment", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TicketComment_Ticket_TicketID",
                        column: x => x.TicketID,
                        principalTable: "Ticket",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketComment_UserAccount_AuthorUserAccountID",
                        column: x => x.AuthorUserAccountID,
                        principalTable: "UserAccount",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketHistory",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketID = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PerformedByUserAccountID = table.Column<int>(type: "int", nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsInternal = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketHistory", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TicketHistory_Ticket_TicketID",
                        column: x => x.TicketID,
                        principalTable: "Ticket",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketHistory_UserAccount_PerformedByUserAccountID",
                        column: x => x.PerformedByUserAccountID,
                        principalTable: "UserAccount",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_AssignedToUserAccountID_IsDeleted_AssignedDate",
                table: "Ticket",
                columns: new[] { "AssignedToUserAccountID", "IsDeleted", "AssignedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_AssignedToUserAccountID_IsDeleted_TicketStatusID",
                table: "Ticket",
                columns: new[] { "AssignedToUserAccountID", "IsDeleted", "TicketStatusID" });

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_ResolvedByUserAccountID",
                table: "Ticket",
                column: "ResolvedByUserAccountID");

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_ResolvedDate",
                table: "Ticket",
                column: "ResolvedDate");

            migrationBuilder.CreateIndex(
                name: "IX_TicketComment_AuthorUserAccountID",
                table: "TicketComment",
                column: "AuthorUserAccountID");

            migrationBuilder.CreateIndex(
                name: "IX_TicketComment_TicketID_IsInternal_IsDeleted_CreatedDate",
                table: "TicketComment",
                columns: new[] { "TicketID", "IsInternal", "IsDeleted", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketHistory_PerformedByUserAccountID",
                table: "TicketHistory",
                column: "PerformedByUserAccountID");

            migrationBuilder.CreateIndex(
                name: "IX_TicketHistory_TicketID_CreatedDate",
                table: "TicketHistory",
                columns: new[] { "TicketID", "CreatedDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_Ticket_UserAccount_ResolvedByUserAccountID",
                table: "Ticket",
                column: "ResolvedByUserAccountID",
                principalTable: "UserAccount",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ticket_UserAccount_ResolvedByUserAccountID",
                table: "Ticket");

            migrationBuilder.DropTable(
                name: "TicketComment");

            migrationBuilder.DropTable(
                name: "TicketHistory");

            migrationBuilder.DropIndex(
                name: "IX_Ticket_AssignedToUserAccountID_IsDeleted_AssignedDate",
                table: "Ticket");

            migrationBuilder.DropIndex(
                name: "IX_Ticket_AssignedToUserAccountID_IsDeleted_TicketStatusID",
                table: "Ticket");

            migrationBuilder.DropIndex(
                name: "IX_Ticket_ResolvedByUserAccountID",
                table: "Ticket");

            migrationBuilder.DropIndex(
                name: "IX_Ticket_ResolvedDate",
                table: "Ticket");

            migrationBuilder.DropColumn(
                name: "ResolutionSummary",
                table: "Ticket");

            migrationBuilder.DropColumn(
                name: "ResolvedByUserAccountID",
                table: "Ticket");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Ticket");
        }
    }
}
