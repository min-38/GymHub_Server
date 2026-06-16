using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymHub.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEntrySupersetGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "superset_group",
                table: "workout_entries",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "superset_group",
                table: "workout_entries");
        }
    }
}
