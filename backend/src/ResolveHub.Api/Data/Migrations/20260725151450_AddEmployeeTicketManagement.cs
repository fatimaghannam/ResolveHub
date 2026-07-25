using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResolveHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeTicketManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketCategory",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketCategory", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TicketPriority",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketPriority", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TicketStatus",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsFinalStatus = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketStatus", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Ticket",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketReferenceNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedByUserAccountID = table.Column<int>(type: "int", nullable: false),
                    AssignedToUserAccountID = table.Column<int>(type: "int", nullable: true),
                    TicketCategoryID = table.Column<int>(type: "int", nullable: false),
                    TicketPriorityID = table.Column<int>(type: "int", nullable: false),
                    TicketStatusID = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssignedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ticket", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Ticket_TicketCategory_TicketCategoryID",
                        column: x => x.TicketCategoryID,
                        principalTable: "TicketCategory",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ticket_TicketPriority_TicketPriorityID",
                        column: x => x.TicketPriorityID,
                        principalTable: "TicketPriority",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ticket_TicketStatus_TicketStatusID",
                        column: x => x.TicketStatusID,
                        principalTable: "TicketStatus",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ticket_UserAccount_AssignedToUserAccountID",
                        column: x => x.AssignedToUserAccountID,
                        principalTable: "UserAccount",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ticket_UserAccount_CreatedByUserAccountID",
                        column: x => x.CreatedByUserAccountID,
                        principalTable: "UserAccount",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_AssignedToUserAccountID",
                table: "Ticket",
                column: "AssignedToUserAccountID");

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_CreatedByUserAccountID",
                table: "Ticket",
                column: "CreatedByUserAccountID");

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_CreatedByUserAccountID_IsDeleted_CreatedDate",
                table: "Ticket",
                columns: new[] { "CreatedByUserAccountID", "IsDeleted", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_CreatedDate",
                table: "Ticket",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_TicketCategoryID",
                table: "Ticket",
                column: "TicketCategoryID");

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_TicketPriorityID",
                table: "Ticket",
                column: "TicketPriorityID");

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_TicketReferenceNumber",
                table: "Ticket",
                column: "TicketReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_TicketStatusID",
                table: "Ticket",
                column: "TicketStatusID");

            migrationBuilder.CreateIndex(
                name: "IX_TicketCategory_Name",
                table: "TicketCategory",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketPriority_Name",
                table: "TicketPriority",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketStatus_Name",
                table: "TicketStatus",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ticket");

            migrationBuilder.DropTable(
                name: "TicketCategory");

            migrationBuilder.DropTable(
                name: "TicketPriority");

            migrationBuilder.DropTable(
                name: "TicketStatus");
        }
    }
}
