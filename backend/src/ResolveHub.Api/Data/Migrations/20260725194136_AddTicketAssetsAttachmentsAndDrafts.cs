using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResolveHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketAssetsAttachmentsAndDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    DepartmentID = table.Column<int>(type: "int", nullable: true),
                    AssignedToUserAccountID = table.Column<int>(type: "int", nullable: true),
                    AssetTag = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AssetName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    AssetType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AssetStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "TicketAttachment",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketID = table.Column<int>(type: "int", nullable: false),
                    UploadedByUserAccountID = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    IsPrivate = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    UploadedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketAttachment", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TicketAttachment_Ticket_TicketID",
                        column: x => x.TicketID,
                        principalTable: "Ticket",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketAttachment_UserAccount_UploadedByUserAccountID",
                        column: x => x.UploadedByUserAccountID,
                        principalTable: "UserAccount",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketDraft",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserAccountID = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true),
                    TicketCategoryID = table.Column<int>(type: "int", nullable: true),
                    TicketPriorityID = table.Column<int>(type: "int", nullable: true),
                    AssetID = table.Column<int>(type: "int", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketDraft", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TicketDraft_Asset_AssetID",
                        column: x => x.AssetID,
                        principalTable: "Asset",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TicketDraft_TicketCategory_TicketCategoryID",
                        column: x => x.TicketCategoryID,
                        principalTable: "TicketCategory",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TicketDraft_TicketPriority_TicketPriorityID",
                        column: x => x.TicketPriorityID,
                        principalTable: "TicketPriority",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TicketDraft_UserAccount_UserAccountID",
                        column: x => x.UserAccountID,
                        principalTable: "UserAccount",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_TicketAttachment_TicketID_IsDeleted",
                table: "TicketAttachment",
                columns: new[] { "TicketID", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketAttachment_UploadedByUserAccountID",
                table: "TicketAttachment",
                column: "UploadedByUserAccountID");

            migrationBuilder.CreateIndex(
                name: "IX_TicketDraft_AssetID",
                table: "TicketDraft",
                column: "AssetID");

            migrationBuilder.CreateIndex(
                name: "IX_TicketDraft_TicketCategoryID",
                table: "TicketDraft",
                column: "TicketCategoryID");

            migrationBuilder.CreateIndex(
                name: "IX_TicketDraft_TicketPriorityID",
                table: "TicketDraft",
                column: "TicketPriorityID");

            migrationBuilder.CreateIndex(
                name: "IX_TicketDraft_UserAccountID_UpdatedDate",
                table: "TicketDraft",
                columns: new[] { "UserAccountID", "UpdatedDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_Ticket_Asset_AssetID",
                table: "Ticket",
                column: "AssetID",
                principalTable: "Asset",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ticket_Asset_AssetID",
                table: "Ticket");

            migrationBuilder.DropTable(
                name: "TicketAttachment");

            migrationBuilder.DropTable(
                name: "TicketDraft");

            migrationBuilder.DropTable(
                name: "Asset");

            migrationBuilder.DropIndex(
                name: "IX_Ticket_AssetID",
                table: "Ticket");

            migrationBuilder.DropColumn(
                name: "AssetID",
                table: "Ticket");
        }
    }
}
