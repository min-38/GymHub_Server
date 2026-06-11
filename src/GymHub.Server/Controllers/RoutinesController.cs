using GymHub.Server.Auth;
using GymHub.Server.Routines;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymHub.Server.Controllers;

[Authorize]
[ApiController]
[Route("routines")]
public sealed class RoutinesController(RoutineService routines) : ControllerBase
{
    private int UserId => User.GetUserId();

    /// <summary>모든 루틴 목록 (종목 포함, order_index 순).</summary>
    [HttpGet]
    public async Task<ActionResult<List<RoutineDto>>> All(CancellationToken ct) =>
        Ok(await routines.GetAllAsync(UserId, ct));

    /// <summary>단일 루틴 조회 (종목 포함). 없으면 404.</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<RoutineDto>> ById(int id, CancellationToken ct)
    {
        var routine = await routines.GetByIdAsync(UserId, id, ct);
        return routine is null ? NotFound() : Ok(routine);
    }

    /// <summary>새 루틴을 생성한다. 이름이 비어 있으면 'untitled'(중복 시 -N)로 생성.</summary>
    [HttpPost]
    public async Task<ActionResult<RoutineDto>> Create([FromBody] CreateRoutineRequest request, CancellationToken ct)
    {
        var routine = string.IsNullOrWhiteSpace(request.Name)
            ? await routines.CreateUntitledAsync(UserId, ct)
            : await routines.CreateAsync(UserId, request.Name, request.Note, ct);
        return Ok(routine);
    }

    /// <summary>운동 세션(id)을 새 루틴으로 저장한다. 세션이 없거나 소유자가 아니면 404.</summary>
    [HttpPost("from-session")]
    public async Task<ActionResult<RoutineDto>> CreateFromSession([FromBody] CreateRoutineFromSessionRequest request, CancellationToken ct)
    {
        var routine = await routines.CreateFromSessionAsync(UserId, request.Name, request.SessionId, ct);
        return routine is null ? NotFound() : Ok(routine);
    }

    /// <summary>루틴 이름/메모를 수정한다.</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Rename(int id, [FromBody] RenameRoutineRequest request, CancellationToken ct) =>
        await routines.RenameAsync(UserId, id, request.Name, request.Note, ct) ? NoContent() : NotFound();

    /// <summary>루틴을 삭제한다 (종목은 cascade로 함께 삭제).</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        await routines.DeleteAsync(UserId, id, ct) ? NoContent() : NotFound();

    /// <summary>루틴에 운동을 추가한다 (기본 3세트 × 10회 × 0kg).</summary>
    [HttpPost("{id}/exercises")]
    public async Task<ActionResult<RoutineExerciseDto>> AddExercise(
        int id, [FromBody] AddRoutineExerciseRequest request, CancellationToken ct)
    {
        var exercise = await routines.AddExerciseAsync(UserId, id, request.ExerciseId, request.ExerciseName, request.Target, ct);
        return exercise is null ? NotFound() : Ok(exercise);
    }

    /// <summary>루틴 종목의 기본 세트 수/횟수/무게를 수정한다.</summary>
    [HttpPut("exercises/{routineExerciseId}/defaults")]
    public async Task<IActionResult> UpdateExerciseDefaults(
        int routineExerciseId, [FromBody] UpdateExerciseDefaultsRequest request, CancellationToken ct) =>
        await routines.UpdateExerciseDefaultsAsync(UserId, routineExerciseId, request.Sets, request.Reps, request.Weight, ct)
            ? NoContent()
            : NotFound();

    /// <summary>루틴에서 종목을 제거한다.</summary>
    [HttpDelete("exercises/{routineExerciseId}")]
    public async Task<IActionResult> RemoveExercise(int routineExerciseId, CancellationToken ct) =>
        await routines.RemoveExerciseAsync(UserId, routineExerciseId, ct) ? NoContent() : NotFound();

    /// <summary>루틴 종목 순서를 주어진 id 순서대로 다시 매긴다.</summary>
    [HttpPut("{id}/exercises/order")]
    public async Task<IActionResult> ReorderExercises(int id, [FromBody] ReorderRoutineExercisesRequest request, CancellationToken ct) =>
        await routines.ReorderExercisesAsync(UserId, id, request.OrderedExerciseIds, ct) ? NoContent() : NotFound();
}

public sealed record CreateRoutineRequest(string? Name, string? Note);

public sealed record CreateRoutineFromSessionRequest(string Name, int SessionId);

public sealed record RenameRoutineRequest(string Name, string? Note);

public sealed record AddRoutineExerciseRequest(string ExerciseId, string ExerciseName, string Target);

public sealed record UpdateExerciseDefaultsRequest(int Sets, int Reps, double Weight);

public sealed record ReorderRoutineExercisesRequest(List<int> OrderedExerciseIds);
