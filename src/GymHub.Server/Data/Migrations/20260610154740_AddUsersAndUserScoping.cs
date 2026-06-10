using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GymHub.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUsersAndUserScoping : Migration
    {
        /// <inheritdoc />
        // Id of the seed "owner" user that inherits all pre-existing single-user data.
        // The first Google sign-in claims this row (see GoogleAuthService), so no
        // personal email is baked into the migration.
        private const int LegacyOwnerId = 1;

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_sessions_date",
                table: "workout_sessions");

            migrationBuilder.DropIndex(
                name: "idx_inbody_date",
                table: "inbody_records");

            migrationBuilder.DropIndex(
                name: "idx_body_date",
                table: "body_measurements");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    email = table.Column<string>(type: "text", nullable: false),
                    google_sub = table.Column<string>(type: "text", nullable: true),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    display_name = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            // Seed the legacy owner and keep the identity sequence past the explicit id.
            migrationBuilder.Sql(
                $"INSERT INTO users (id, email, display_name) " +
                $"VALUES ({LegacyOwnerId}, 'owner@gymhub.local', 'Owner') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql(
                "SELECT setval(pg_get_serial_sequence('users', 'id'), (SELECT MAX(id) FROM users));");

            migrationBuilder.AddColumn<int>(
                name: "user_id",
                table: "workout_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "user_id",
                table: "routines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "user_id",
                table: "inbody_records",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "user_id",
                table: "body_measurements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill all pre-existing rows to the legacy owner before the FKs apply.
            foreach (var table in new[] { "workout_sessions", "routines", "inbody_records", "body_measurements" })
            {
                migrationBuilder.Sql($"UPDATE {table} SET user_id = {LegacyOwnerId} WHERE user_id = 0;");
            }

            migrationBuilder.CreateIndex(
                name: "idx_sessions_user_date",
                table: "workout_sessions",
                columns: new[] { "user_id", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_routines_user",
                table: "routines",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_inbody_user_date",
                table: "inbody_records",
                columns: new[] { "user_id", "date" });

            migrationBuilder.CreateIndex(
                name: "idx_body_user_date",
                table: "body_measurements",
                columns: new[] { "user_id", "date" });

            migrationBuilder.CreateIndex(
                name: "idx_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_users_google_sub",
                table: "users",
                column: "google_sub",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_body_measurements_users_user_id",
                table: "body_measurements",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_inbody_records_users_user_id",
                table: "inbody_records",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_routines_users_user_id",
                table: "routines",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_workout_sessions_users_user_id",
                table: "workout_sessions",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_body_measurements_users_user_id",
                table: "body_measurements");

            migrationBuilder.DropForeignKey(
                name: "FK_inbody_records_users_user_id",
                table: "inbody_records");

            migrationBuilder.DropForeignKey(
                name: "FK_routines_users_user_id",
                table: "routines");

            migrationBuilder.DropForeignKey(
                name: "FK_workout_sessions_users_user_id",
                table: "workout_sessions");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropIndex(
                name: "idx_sessions_user_date",
                table: "workout_sessions");

            migrationBuilder.DropIndex(
                name: "idx_routines_user",
                table: "routines");

            migrationBuilder.DropIndex(
                name: "idx_inbody_user_date",
                table: "inbody_records");

            migrationBuilder.DropIndex(
                name: "idx_body_user_date",
                table: "body_measurements");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "workout_sessions");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "routines");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "inbody_records");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "body_measurements");

            migrationBuilder.CreateIndex(
                name: "idx_sessions_date",
                table: "workout_sessions",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "idx_inbody_date",
                table: "inbody_records",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "idx_body_date",
                table: "body_measurements",
                column: "date");
        }
    }
}
