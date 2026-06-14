using GymHub.Server.Auth;
using GymHub.Server.Stats;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymHub.Server.Controllers;

[Authorize]
[ApiController]
[Route("stats")]
public sealed class StatsController(StatsService stats) : ControllerBase
{
    private int UserId => User.GetUserId();

    /// <summary>부위별 볼륨/세트/Reps. 현재와 직전 기간(주/월)을 함께 반환한다. (App #12)</summary>
    [HttpGet("body-part-volume")]
    public async Task<ActionResult<BodyPartVolumeResponse>> BodyPartVolume(
        [FromQuery] string period, [FromQuery] DateOnly? today, CancellationToken ct) =>
        Ok(await stats.GetBodyPartVolumeAsync(UserId, period, today ?? Today, ct));

    /// <summary>최근 7일 부위별 근육 피로도(0–100). (App #12)</summary>
    [HttpGet("fatigue")]
    public async Task<ActionResult<FatigueResponse>> Fatigue(
        [FromQuery] DateOnly? today, CancellationToken ct) =>
        Ok(await stats.GetFatigueAsync(UserId, today ?? Today, ct));

    /// <summary>이번 주 vs 지난 주 요일별 볼륨/세트/Reps 추이 + 총합. (App #13)</summary>
    [HttpGet("weekly-trend")]
    public async Task<ActionResult<WeeklyTrendResponse>> WeeklyTrend(
        [FromQuery] DateOnly? today, CancellationToken ct) =>
        Ok(await stats.GetWeeklyTrendAsync(UserId, today ?? Today, ct));

    /// <summary>종목별 분석: 예상 1RM·최고무게·최대볼륨 + 세션별 과부하 추이. (App #17)</summary>
    [HttpGet("exercise/{exerciseId}")]
    public async Task<ActionResult<ExerciseAnalysisResponse>> Exercise(
        string exerciseId, CancellationToken ct) =>
        Ok(await stats.GetExerciseAnalysisAsync(UserId, exerciseId, ct));

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}
