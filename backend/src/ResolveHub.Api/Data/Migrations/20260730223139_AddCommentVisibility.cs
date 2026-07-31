using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResolveHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommentVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TicketComment_TicketID_IsInternal_IsDeleted_CreatedDate",
                table: "TicketComment");

            migrationBuilder.AddColumn<string>(
                name: "Visibility",
                table: "TicketComment",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Public");

            migrationBuilder.Sql(
                "UPDATE [TicketComment] SET [Visibility] = CASE WHEN [IsInternal] = 1 THEN 'Private' ELSE 'Public' END");

            migrationBuilder.DropColumn(
                name: "IsInternal",
                table: "TicketComment");

            migrationBuilder.CreateIndex(
                name: "IX_TicketComment_TicketID_Visibility_IsDeleted_CreatedDate",
                table: "TicketComment",
                columns: new[] { "TicketID", "Visibility", "IsDeleted", "CreatedDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TicketComment_TicketID_Visibility_IsDeleted_CreatedDate",
                table: "TicketComment");

            migrationBuilder.AddColumn<bool>(
                name: "IsInternal",
                table: "TicketComment",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE [TicketComment] SET [IsInternal] = CASE WHEN [Visibility] = 'Private' THEN 1 ELSE 0 END");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "TicketComment");

            migrationBuilder.CreateIndex(
                name: "IX_TicketComment_TicketID_IsInternal_IsDeleted_CreatedDate",
                table: "TicketComment",
                columns: new[] { "TicketID", "IsInternal", "IsDeleted", "CreatedDate" });
        }
    }
}
