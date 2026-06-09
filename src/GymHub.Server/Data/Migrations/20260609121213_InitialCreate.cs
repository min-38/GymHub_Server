using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GymHub.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_meta",
                columns: table => new
                {
                    key = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_meta", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "body_measurements",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    weight = table.Column<double>(type: "double precision", nullable: false),
                    height = table.Column<double>(type: "double precision", nullable: true),
                    chest = table.Column<double>(type: "double precision", nullable: true),
                    waist = table.Column<double>(type: "double precision", nullable: true),
                    hip = table.Column<double>(type: "double precision", nullable: true),
                    arm = table.Column<double>(type: "double precision", nullable: true),
                    thigh = table.Column<double>(type: "double precision", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_body_measurements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "exercise_name_overrides",
                columns: table => new
                {
                    exercise_id = table.Column<string>(type: "text", nullable: false),
                    name_ko = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_name_overrides", x => x.exercise_id);
                });

            migrationBuilder.CreateTable(
                name: "exercises",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    name_ko = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    body_part = table.Column<string>(type: "text", nullable: false),
                    body_part_ko = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    target = table.Column<string>(type: "text", nullable: false),
                    target_ko = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    equipment = table.Column<string>(type: "text", nullable: false),
                    equipment_ko = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    gif_url = table.Column<string>(type: "text", nullable: false),
                    secondary_muscles = table.Column<string>(type: "text", nullable: true),
                    instructions = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    gif_local_path = table.Column<string>(type: "text", nullable: true),
                    gif_cached_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercises", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inbody_records",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    weight = table.Column<double>(type: "double precision", nullable: false),
                    body_fat_percentage = table.Column<double>(type: "double precision", nullable: true),
                    muscle_mass = table.Column<double>(type: "double precision", nullable: true),
                    bmi = table.Column<double>(type: "double precision", nullable: true),
                    body_water = table.Column<double>(type: "double precision", nullable: true),
                    bmr = table.Column<double>(type: "double precision", nullable: true),
                    visceral_fat = table.Column<int>(type: "integer", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbody_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "routines",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    order_index = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_routines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workout_sessions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    duration_sec = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workout_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "routine_exercises",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    routine_id = table.Column<int>(type: "integer", nullable: false),
                    exercise_id = table.Column<string>(type: "text", nullable: false),
                    exercise_name = table.Column<string>(type: "text", nullable: false),
                    target = table.Column<string>(type: "text", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    default_sets = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    default_reps = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    default_weight = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_routine_exercises", x => x.id);
                    table.ForeignKey(
                        name: "FK_routine_exercises_routines_routine_id",
                        column: x => x.routine_id,
                        principalTable: "routines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workout_entries",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<int>(type: "integer", nullable: false),
                    exercise_id = table.Column<string>(type: "text", nullable: false),
                    exercise_name = table.Column<string>(type: "text", nullable: false),
                    target = table.Column<string>(type: "text", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workout_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_workout_entries_workout_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "workout_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workout_sets",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entry_id = table.Column<int>(type: "integer", nullable: false),
                    set_number = table.Column<int>(type: "integer", nullable: false),
                    weight = table.Column<double>(type: "double precision", nullable: false),
                    reps = table.Column<int>(type: "integer", nullable: false),
                    completed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workout_sets", x => x.id);
                    table.ForeignKey(
                        name: "FK_workout_sets_workout_entries_entry_id",
                        column: x => x.entry_id,
                        principalTable: "workout_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_body_date",
                table: "body_measurements",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "idx_exercises_body_part",
                table: "exercises",
                column: "body_part");

            migrationBuilder.CreateIndex(
                name: "idx_exercises_body_part_ko",
                table: "exercises",
                column: "body_part_ko");

            migrationBuilder.CreateIndex(
                name: "idx_exercises_equipment",
                table: "exercises",
                column: "equipment");

            migrationBuilder.CreateIndex(
                name: "idx_exercises_name_ko",
                table: "exercises",
                column: "name_ko");

            migrationBuilder.CreateIndex(
                name: "idx_exercises_target",
                table: "exercises",
                column: "target");

            migrationBuilder.CreateIndex(
                name: "idx_inbody_date",
                table: "inbody_records",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "idx_routine_exercises_routine",
                table: "routine_exercises",
                column: "routine_id");

            migrationBuilder.CreateIndex(
                name: "idx_entries_exercise",
                table: "workout_entries",
                column: "exercise_id");

            migrationBuilder.CreateIndex(
                name: "idx_entries_session",
                table: "workout_entries",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "idx_entries_target",
                table: "workout_entries",
                column: "target");

            migrationBuilder.CreateIndex(
                name: "idx_sessions_date",
                table: "workout_sessions",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "idx_sets_entry",
                table: "workout_sets",
                column: "entry_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_meta");

            migrationBuilder.DropTable(
                name: "body_measurements");

            migrationBuilder.DropTable(
                name: "exercise_name_overrides");

            migrationBuilder.DropTable(
                name: "exercises");

            migrationBuilder.DropTable(
                name: "inbody_records");

            migrationBuilder.DropTable(
                name: "routine_exercises");

            migrationBuilder.DropTable(
                name: "workout_sets");

            migrationBuilder.DropTable(
                name: "routines");

            migrationBuilder.DropTable(
                name: "workout_entries");

            migrationBuilder.DropTable(
                name: "workout_sessions");
        }
    }
}
