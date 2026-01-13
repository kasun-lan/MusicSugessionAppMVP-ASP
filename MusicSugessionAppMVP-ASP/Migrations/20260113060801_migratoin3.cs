using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicSugessionAppMVP_ASP.Migrations
{
    /// <inheritdoc />
    public partial class migratoin3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Users_UserId",
                table: "UserRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserRoles",
                table: "UserRoles");

            migrationBuilder.RenameTable(
                name: "UserRoles",
                newName: "UserRoleAssignments");

            migrationBuilder.RenameIndex(
                name: "IX_UserRoles_UserId",
                table: "UserRoleAssignments",
                newName: "IX_UserRoleAssignments_UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserRoleAssignments",
                table: "UserRoleAssignments",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoleAssignments_Users_UserId",
                table: "UserRoleAssignments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserRoleAssignments_Users_UserId",
                table: "UserRoleAssignments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserRoleAssignments",
                table: "UserRoleAssignments");

            migrationBuilder.RenameTable(
                name: "UserRoleAssignments",
                newName: "UserRoles");

            migrationBuilder.RenameIndex(
                name: "IX_UserRoleAssignments_UserId",
                table: "UserRoles",
                newName: "IX_UserRoles_UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserRoles",
                table: "UserRoles",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_Users_UserId",
                table: "UserRoles",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
