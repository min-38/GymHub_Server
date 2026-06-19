using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymHub.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionStatusAndCompletedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "completed_at",
                table: "workout_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "workout_sessions",
                type: "text",
                nullable: false,
                defaultValue: "in_progress");

            // 기존 세션 중 운동 시간이 기록된(duration_sec > 0) 것은 완료로 간주해 백필한다.
            // (이전엔 duration_sec > 0 을 완료 프록시로 사용했음.)
            migrationBuilder.Sql(
                "UPDATE workout_sessions SET status = 'completed' WHERE duration_sec > 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "completed_at",
                table: "workout_sessions");

            migrationBuilder.DropColumn(
                name: "status",
                table: "workout_sessions");
        }
    }
}
