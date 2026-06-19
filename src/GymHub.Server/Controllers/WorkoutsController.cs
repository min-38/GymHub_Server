using GymHub.Server.Auth;
using GymHub.Server.Workouts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymHub.Server.Controllers;

[Authorize]
[ApiController]
[Route("workouts")]
public sealed class WorkoutsController(WorkoutService workouts) : ControllerBase
{
    private int UserId => User.GetUserId();

    /// <summary>모든 세션 목록 (날짜 내림차순).</summary>
    [HttpGet("sessions")]
    public async Task<ActionResult<List<WorkoutSessionDto>>> Sessions(CancellationToken ct) =>
        Ok(await workouts.GetSessionsAsync(UserId, ct));

    /// <summary>운동(종목)이 1개 이상 기록된 날짜 목록. 달력 하이라이트용.</summary>
    [HttpGet("dates")]
    public async Task<ActionResult<List<DateOnly>>> Dates(CancellationToken ct) =>
        Ok(await workouts.GetWorkoutDatesAsync(UserId, ct));

    /// <summary>해당 날짜의 종목+세트. 세션이 없으면 빈 배열.</summary>
    [HttpGet("sessions/by-date/{date}")]
    public async Task<ActionResult<List<WorkoutEntryDto>>> EntriesOfDate(DateOnly date, CancellationToken ct) =>
        Ok(await workouts.GetEntriesOfDateAsync(UserId, date, ct));

    /// <summary>해당 날짜의 세션을 찾고, 없으면 생성한다.</summary>
    [HttpPost("sessions")]
    public async Task<ActionResult<WorkoutSessionDto>> EnsureSession([FromBody] EnsureSessionRequest request, CancellationToken ct) =>
        Ok(await workouts.EnsureSessionAsync(UserId, request.Date, ct));

    /// <summary>세션 메모/운동 시간을 갱신한다.</summary>
    [HttpPatch("sessions/{id}")]
    public async Task<IActionResult> UpdateSession(int id, [FromBody] UpdateSessionRequest request, CancellationToken ct) =>
        await workouts.UpdateSessionAsync(UserId, id, request.Note, request.DurationSec, ct) ? NoContent() : NotFound();

    /// <summary>세션을 완료 처리한다. status=completed, 완료 시각·측정된 운동 시간을 저장.</summary>
    [HttpPost("sessions/{id}/complete")]
    public async Task<ActionResult<WorkoutSessionDto>> CompleteSession(int id, [FromBody] CompleteSessionRequest request, CancellationToken ct)
    {
        var session = await workouts.CompleteSessionAsync(UserId, id, request.DurationSec, ct);
        return session is null ? NotFound() : Ok(session);
    }

    /// <summary>세션을 삭제한다 (종목/세트는 cascade로 함께 삭제).</summary>
    [HttpDelete("sessions/{id}")]
    public async Task<IActionResult> DeleteSession(int id, CancellationToken ct) =>
        await workouts.DeleteSessionAsync(UserId, id, ct) ? NoContent() : NotFound();

    /// <summary>
    /// 세션에 운동을 추가한다. 이전 기록이 있으면 그 세트를 복사해 프리필,
    /// 없으면 빈 세트 1개를 만든다.
    /// </summary>
    [HttpPost("sessions/{sessionId}/entries")]
    public async Task<ActionResult<WorkoutEntryDto>> AddExercise(
        int sessionId, [FromBody] AddExerciseRequest request, CancellationToken ct)
    {
        var entry = await workouts.AddExerciseAsync(UserId, sessionId, request.ExerciseId, request.ExerciseName, request.Target, ct);
        return entry is null ? NotFound() : Ok(entry);
    }

    /// <summary>entry의 종목(운동 id/이름/부위)을 교체한다. 세트·순서는 유지.</summary>
    [HttpPut("entries/{entryId}/exercise")]
    public async Task<ActionResult<WorkoutEntryDto>> ChangeEntryExercise(
        int entryId, [FromBody] AddExerciseRequest request, CancellationToken ct)
    {
        var entry = await workouts.ChangeEntryExerciseAsync(UserId, entryId, request.ExerciseId, request.ExerciseName, request.Target, ct);
        return entry is null ? NotFound() : Ok(entry);
    }

    /// <summary>종목별 휴식 시간(초)을 설정한다. restSec=null이면 전역 기본값을 따른다.</summary>
    [HttpPatch("entries/{entryId}")]
    public async Task<IActionResult> UpdateEntry(int entryId, [FromBody] UpdateEntryRequest request, CancellationToken ct) =>
        await workouts.SetEntryRestAsync(UserId, entryId, request.RestSec, ct) ? NoContent() : NotFound();

    /// <summary>entry의 슈퍼세트 그룹을 설정한다. supersetGroup=null이면 그룹에서 해제.</summary>
    [HttpPatch("entries/{entryId}/superset")]
    public async Task<IActionResult> UpdateEntrySuperset(int entryId, [FromBody] UpdateSupersetRequest request, CancellationToken ct) =>
        await workouts.SetEntrySupersetAsync(UserId, entryId, request.SupersetGroup, ct) ? NoContent() : NotFound();

    /// <summary>종목을 삭제한다 (세트는 cascade로 함께 삭제).</summary>
    [HttpDelete("entries/{entryId}")]
    public async Task<IActionResult> DeleteEntry(int entryId, CancellationToken ct) =>
        await workouts.DeleteEntryAsync(UserId, entryId, ct) ? NoContent() : NotFound();

    /// <summary>종목 순서를 주어진 id 순서대로 다시 매긴다.</summary>
    [HttpPut("sessions/{sessionId}/entries/order")]
    public async Task<IActionResult> ReorderEntries(int sessionId, [FromBody] ReorderEntriesRequest request, CancellationToken ct) =>
        await workouts.ReorderEntriesAsync(UserId, sessionId, request.OrderedEntryIds, ct) ? NoContent() : NotFound();

    /// <summary>종목에 새 세트를 추가한다 (set_number는 자동 증가).</summary>
    [HttpPost("entries/{entryId}/sets")]
    public async Task<ActionResult<WorkoutSetDto>> AddSet(int entryId, [FromBody] AddSetRequest request, CancellationToken ct)
    {
        var set = await workouts.AddSetAsync(UserId, entryId, request.Weight, request.Reps, ct);
        return set is null ? NotFound() : Ok(set);
    }

    /// <summary>세트의 무게/횟수/세트번호/완료 여부를 갱신한다.</summary>
    [HttpPut("sets/{setId}")]
    public async Task<IActionResult> UpdateSet(int setId, [FromBody] UpdateSetRequest request, CancellationToken ct) =>
        await workouts.UpdateSetAsync(UserId, setId, request.Weight, request.Reps, request.SetNumber, request.Completed, ct)
            ? NoContent()
            : NotFound();

    /// <summary>세트를 삭제한다.</summary>
    [HttpDelete("sets/{setId}")]
    public async Task<IActionResult> DeleteSet(int setId, CancellationToken ct) =>
        await workouts.DeleteSetAsync(UserId, setId, ct) ? NoContent() : NotFound();

    /// <summary>한 날짜의 모든 종목·세트를 다른 날짜 세션으로 복사(병합)한다.</summary>
    [HttpPost("copy")]
    public async Task<ActionResult<CopyEntriesResponse>> CopyEntries([FromBody] CopyEntriesRequest request, CancellationToken ct) =>
        Ok(new CopyEntriesResponse(await workouts.CopyEntriesAsync(UserId, request.FromDate, request.ToDate, ct)));

    /// <summary>해당 운동의 가장 최근 기록(세트 포함). 없으면 404.</summary>
    [HttpGet("last-record/{exerciseId}")]
    public async Task<ActionResult<LastRecordDto>> LastRecord(string exerciseId, [FromQuery] int? excludeSessionId, CancellationToken ct)
    {
        var record = await workouts.GetLastRecordAsync(UserId, exerciseId, excludeSessionId, ct);
        return record is null ? NotFound() : Ok(record);
    }
}

public sealed record EnsureSessionRequest(DateOnly Date);

public sealed record UpdateSessionRequest(string? Note, int? DurationSec);

public sealed record CompleteSessionRequest(int DurationSec);

public sealed record AddExerciseRequest(string ExerciseId, string ExerciseName, string Target);

public sealed record UpdateEntryRequest(int? RestSec);

public sealed record UpdateSupersetRequest(int? SupersetGroup);

public sealed record ReorderEntriesRequest(List<int> OrderedEntryIds);

public sealed record AddSetRequest(double Weight = 0, int Reps = 0);

public sealed record UpdateSetRequest(double? Weight, int? Reps, int? SetNumber, bool? Completed);

public sealed record CopyEntriesRequest(DateOnly FromDate, DateOnly ToDate);

public sealed record CopyEntriesResponse(int Copied);
