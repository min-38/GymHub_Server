using GymHub.Server.Exercises;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymHub.Server.Controllers;

[ApiController]
[Route("exercises")]
public sealed class ExercisesController(ExerciseService exercises) : ControllerBase
{
    private const int DefaultLimit = 100;
    private const int MaxLimit = 200;

    /// <summary>운동 목록 조회. 검색 + 부위/타깃/장비 필터 + 페이지네이션.</summary>
    [HttpGet]
    public async Task<ActionResult<ExercisePage>> List(
        [FromQuery] string? search,
        [FromQuery] string? bodyPart,
        [FromQuery] string? target,
        [FromQuery] string? equipment,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken ct)
    {
        var query = new ExerciseQuery(
            search,
            bodyPart,
            target,
            equipment,
            Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit),
            Math.Max(offset ?? 0, 0));

        return Ok(await exercises.QueryAsync(query, ct));
    }

    /// <summary>단일 운동 조회.</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ExerciseDto>> Get(string id, CancellationToken ct)
    {
        var exercise = await exercises.GetByIdAsync(id, ct);
        return exercise is null ? NotFound() : Ok(exercise);
    }

    /// <summary>한글명 오버라이드 전체 조회(운동 id → 한글명).</summary>
    [HttpGet("name-overrides")]
    public async Task<ActionResult<Dictionary<string, string>>> NameOverrides(CancellationToken ct) =>
        Ok(await exercises.GetNameOverridesAsync(ct));

    /// <summary>한글명 오버라이드 설정. nameKo가 비어 있으면 오버라이드를 제거한다.</summary>
    [Authorize]
    [HttpPut("{id}/name-override")]
    public async Task<IActionResult> SetNameOverride(
        string id,
        [FromBody] NameOverrideRequest request,
        CancellationToken ct)
    {
        var ok = await exercises.SetNameOverrideAsync(id, request.NameKo, ct);
        return ok ? NoContent() : NotFound();
    }
}

public sealed record NameOverrideRequest(string? NameKo);
