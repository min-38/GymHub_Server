using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymHub.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEntryRestSec : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "rest_sec",
                table: "workout_entries",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "rest_sec",
                table: "workout_entries");
        }
    }
}
