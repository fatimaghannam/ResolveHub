using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResolveHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAssetManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ticket_Asset_AssetID",
                table: "Ticket");

            migrationBuilder.DropForeignKey(
                name: "FK_TicketDraft_Asset_AssetID",
                table: "TicketDraft");

            migrationBuilder.DropTable(
                name: "Asset");

            migrationBuilder.DropIndex(
                name: "IX_TicketDraft_AssetID",
                table: "TicketDraft");

            migrationBuilder.DropIndex(
                name: "IX_Ticket_AssetID",
                table: "Ticket");

            migrationBuilder.DropColumn(
                name: "AssetID",
                table: "TicketDraft");

            migrationBuilder.DropColumn(
                name: "AssetID",
                table: "Ticket");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssetID",
                table: "TicketDraft",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssetID",
                table: "Ticket",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Asset",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssignedToUserAccountID = table.Column<int>(type: "int", nullable: true),
                    DepartmentID = table.Column<int>(type: "int", nullable: true),
                    AssetName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    AssetStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AssetTag = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AssetType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Location = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Asset", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Asset_Department_DepartmentID",
                        column: x => x.DepartmentID,
                        principalTable: "Department",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Asset_UserAccount_AssignedToUserAccountID",
                        column: x => x.AssignedToUserAccountID,
                        principalTable: "UserAccount",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketDraft_AssetID",
                table: "TicketDraft",
                column: "AssetID");

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_AssetID",
                table: "Ticket",
                column: "AssetID");

            migrationBuilder.CreateIndex(
                name: "IX_Asset_AssetTag",
                table: "Asset",
                column: "AssetTag",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Asset_AssignedToUserAccountID",
                table: "Asset",
                column: "AssignedToUserAccountID");

            migrationBuilder.CreateIndex(
                name: "IX_Asset_DepartmentID",
                table: "Asset",
                column: "DepartmentID");

            migrationBuilder.AddForeignKey(
                name: "FK_Ticket_Asset_AssetID",
                table: "Ticket",
                column: "AssetID",
                principalTable: "Asset",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketDraft_Asset_AssetID",
                table: "TicketDraft",
                column: "AssetID",
                principalTable: "Asset",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
