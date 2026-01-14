using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicSugessionAppMVP_ASP.Migrations
{
    /// <inheritdoc />
    public partial class migratoin4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SessionId1",
                table: "SessionExports",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionExports_SessionId1",
                table: "SessionExports",
                column: "SessionId1");

            migrationBuilder.AddForeignKey(
                name: "FK_SessionExports_Sessions_SessionId1",
                table: "SessionExports",
                column: "SessionId1",
                principalTable: "Sessions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SessionExports_Sessions_SessionId1",
                table: "SessionExports");

            migrationBuilder.DropIndex(
                name: "IX_SessionExports_SessionId1",
                table: "SessionExports");

            migrationBuilder.DropColumn(
                name: "SessionId1",
                table: "SessionExports");
        }
    }
}
