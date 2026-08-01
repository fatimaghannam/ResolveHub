using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResolveHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncThreadedTicketCommentModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedDate",
                table: "TicketComment",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentCommentID",
                table: "TicketComment",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketComment_ParentCommentID",
                table: "TicketComment",
                column: "ParentCommentID");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketComment_TicketComment_ParentCommentID",
                table: "TicketComment",
                column: "ParentCommentID",
                principalTable: "TicketComment",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketComment_TicketComment_ParentCommentID",
                table: "TicketComment");

            migrationBuilder.DropIndex(
                name: "IX_TicketComment_ParentCommentID",
                table: "TicketComment");

            migrationBuilder.DropColumn(name: "DeletedDate", table: "TicketComment");
            migrationBuilder.DropColumn(name: "ParentCommentID", table: "TicketComment");
        }
    }
}
