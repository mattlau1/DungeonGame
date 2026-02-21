using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DungeonServer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EfPlayerStoreInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOnline",
                table: "Players",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOnline",
                table: "Players");
        }
    }
}
