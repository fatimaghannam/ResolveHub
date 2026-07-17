using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResolveHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixUserAccountRoleKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserAccountRole",
                table: "UserAccountRole");

            migrationBuilder.DropIndex(
                name: "IX_UserAccountRole_UserAccountID_RoleID",
                table: "UserAccountRole");

            migrationBuilder.DropColumn(
                name: "ID",
                table: "UserAccountRole");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserAccountRole",
                table: "UserAccountRole",
                columns: new[] { "UserAccountID", "RoleID" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserAccountRole",
                table: "UserAccountRole");

            migrationBuilder.AddColumn<int>(
                name: "ID",
                table: "UserAccountRole",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserAccountRole",
                table: "UserAccountRole",
                column: "ID");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccountRole_UserAccountID_RoleID",
                table: "UserAccountRole",
                columns: new[] { "UserAccountID", "RoleID" },
                unique: true);
        }
    }
}
