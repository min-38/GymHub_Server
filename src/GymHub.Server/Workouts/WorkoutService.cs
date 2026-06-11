using GymHub.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace GymHub.Server.Workouts;

/// <summary>
/// CRUD for the authenticated user's workout sessions/entries/sets.
/// All reads and writes are scoped to <c>userId</c> (sessions own that
/// foreign key; entries and sets are reached through their session).
/// </summary>
public sealed class WorkoutService(GymHubDbContext db)
{
    public async Task<List<WorkoutSessionDto>> GetSessionsAsync(int userId, CancellationToken ct) =>
        await db.WorkoutSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.Date)
            .ThenByDescending(s => s.Id)
            .Select(ProjectSession)
            .ToListAsync(ct);

    /// <summary>Dates with at least one entry. Used for calendar highlights.</summary>
    public async Task<List<DateOnly>> GetWorkoutDatesAsync(int userId, CancellationToken ct) =>
        await db.WorkoutSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Entries.Any())
            .Select(s => s.Date)
            .Distinct()
            .ToListAsync(ct);

    /// <summary>Entries (with sets) for the session on this date, or empty if no session exists.</summary>
    public async Task<List<WorkoutEntryDto>> GetEntriesOfDateAsync(int userId, DateOnly date, CancellationToken ct)
    {
        var sessionId = await db.WorkoutSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Date == date)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(ct);

        return sessionId is null ? [] : await LoadEntriesAsync(sessionId.Value, ct);
    }

    /// <summary>Finds the session for this date, creating an empty one if needed.</summary>
    public async Task<WorkoutSessionDto> EnsureSessionAsync(int userId, DateOnly date, CancellationToken ct)
    {
        var existing = await db.WorkoutSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Date == date)
            .Select(ProjectSession)
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            return existing;
        }

        var session = new WorkoutSession { UserId = userId, Date = date };
        db.WorkoutSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return new WorkoutSessionDto(session.Id, session.Date, session.Note, session.DurationSec);
    }

    /// <summary>Updates the session's note and/or measured duration. Returns false if not found/owned.</summary>
    public async Task<bool> UpdateSessionAsync(int userId, int sessionId, string? note, int? durationSec, CancellationToken ct)
    {
        var session = await db.WorkoutSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, ct);
        if (session is null)
        {
            return false;
        }

        if (note is not null)
        {
            session.Note = note;
        }

        if (durationSec is not null)
        {
            session.DurationSec = durationSec.Value;
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Deletes a session and (via FK cascade) its entries and sets.</summary>
    public async Task<bool> DeleteSessionAsync(int userId, int sessionId, CancellationToken ct)
    {
        var session = await db.WorkoutSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, ct);
        if (session is null)
        {
            return false;
        }

        db.WorkoutSessions.Remove(session);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Adds an exercise to the session. Prefills sets from the user's most recent
    /// record of this exercise (excluding this session), or one empty set if none exists.
    /// Returns null if the session does not exist or is not owned by the user.
    /// </summary>
    public async Task<WorkoutEntryDto?> AddExerciseAsync(
        int userId, int sessionId, string exerciseId, string exerciseName, string target, CancellationToken ct)
    {
        var sessionOwned = await db.WorkoutSessions.AnyAsync(s => s.Id == sessionId && s.UserId == userId, ct);
        if (!sessionOwned)
        {
            return null;
        }

        var entry = await AddEntryEntityAsync(sessionId, exerciseId, exerciseName, target, ct);

        var lastSets = await db.WorkoutSets
            .AsNoTracking()
            .Where(set => set.Entry!.ExerciseId == exerciseId
                && set.Entry.Session!.UserId == userId
                && set.Entry.SessionId != sessionId)
            .OrderByDescending(set => set.Entry!.Session!.Date)
            .ThenByDescending(set => set.EntryId)
            .ThenBy(set => set.SetNumber)
            .Select(set => new { set.EntryId, set.Weight, set.Reps })
            .ToListAsync(ct);

        var lastEntryId = lastSets.Select(s => s.EntryId).FirstOrDefault();
        var setsToCopy = lastSets.Where(s => s.EntryId == lastEntryId).ToList();

        if (setsToCopy.Count > 0)
        {
            foreach (var s in setsToCopy)
            {
                await AddSetEntityAsync(entry.Id, s.Weight, s.Reps, ct);
            }
        }
        else
        {
            await AddSetEntityAsync(entry.Id, 0, 0, ct);
        }

        return (await LoadEntriesAsync(sessionId, ct)).First(e => e.Id == entry.Id);
    }

    public async Task<bool> DeleteEntryAsync(int userId, int entryId, CancellationToken ct)
    {
        var entry = await db.WorkoutEntries.FirstOrDefaultAsync(e => e.Id == entryId && e.Session!.UserId == userId, ct);
        if (entry is null)
        {
            return false;
        }

        db.WorkoutEntries.Remove(entry);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Re-numbers entry order_index to match the given order. Returns false if not found/owned.</summary>
    public async Task<bool> ReorderEntriesAsync(int userId, int sessionId, List<int> orderedEntryIds, CancellationToken ct)
    {
        var sessionOwned = await db.WorkoutSessions.AnyAsync(s => s.Id == sessionId && s.UserId == userId, ct);
        if (!sessionOwned)
        {
            return false;
        }

        var entries = await db.WorkoutEntries
            .Where(e => e.SessionId == sessionId && orderedEntryIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, ct);

        for (var i = 0; i < orderedEntryIds.Count; i++)
        {
            if (entries.TryGetValue(orderedEntryIds[i], out var entry))
            {
                entry.OrderIndex = i;
            }
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<WorkoutSetDto?> AddSetAsync(int userId, int entryId, double weight, int reps, CancellationToken ct)
    {
        var entryOwned = await db.WorkoutEntries.AnyAsync(e => e.Id == entryId && e.Session!.UserId == userId, ct);
        if (!entryOwned)
        {
            return null;
        }

        var set = await AddSetEntityAsync(entryId, weight, reps, ct);
        return ProjectSetDto(set);
    }

    public async Task<bool> UpdateSetAsync(
        int userId, int setId, double? weight, int? reps, int? setNumber, bool? completed, CancellationToken ct)
    {
        var set = await db.WorkoutSets.FirstOrDefaultAsync(s => s.Id == setId && s.Entry!.Session!.UserId == userId, ct);
        if (set is null)
        {
            return false;
        }

        if (weight is not null) set.Weight = weight.Value;
        if (reps is not null) set.Reps = reps.Value;
        if (setNumber is not null) set.SetNumber = setNumber.Value;
        if (completed is not null) set.Completed = completed.Value;

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteSetAsync(int userId, int setId, CancellationToken ct)
    {
        var set = await db.WorkoutSets.FirstOrDefaultAsync(s => s.Id == setId && s.Entry!.Session!.UserId == userId, ct);
        if (set is null)
        {
            return false;
        }

        db.WorkoutSets.Remove(set);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Copies all entries (and their sets, verbatim) from <paramref name="fromDate"/> into the
    /// session for <paramref name="toDate"/> (creating it if needed). Returns the number of
    /// entries copied; 0 if there is nothing to copy.
    /// </summary>
    public async Task<int> CopyEntriesAsync(int userId, DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        var source = await GetEntriesOfDateAsync(userId, fromDate, ct);
        if (source.Count == 0)
        {
            return 0;
        }

        var target = await EnsureSessionAsync(userId, toDate, ct);

        foreach (var e in source)
        {
            var entry = await AddEntryEntityAsync(target.Id, e.ExerciseId, e.ExerciseName, e.Target, ct);
            foreach (var s in e.Sets)
            {
                await AddSetEntityAsync(entry.Id, s.Weight, s.Reps, ct);
            }
        }

        return source.Count;
    }

    /// <summary>
    /// The user's most recent record (date + sets) of this exercise, optionally excluding
    /// one session (the one currently being edited). Null if there is no prior record.
    /// </summary>
    public async Task<LastRecordDto?> GetLastRecordAsync(int userId, string exerciseId, int? excludeSessionId, CancellationToken ct)
    {
        var entry = await db.WorkoutEntries
            .AsNoTracking()
            .Where(e => e.ExerciseId == exerciseId
                && e.Session!.UserId == userId
                && (excludeSessionId == null || e.SessionId != excludeSessionId))
            .OrderByDescending(e => e.Session!.Date)
            .ThenByDescending(e => e.Id)
            .Select(e => new { e.Id, e.Session!.Date })
            .FirstOrDefaultAsync(ct);

        if (entry is null)
        {
            return null;
        }

        var sets = await db.WorkoutSets
            .AsNoTracking()
            .Where(s => s.EntryId == entry.Id)
            .OrderBy(s => s.SetNumber)
            .Select(ProjectSetExpr)
            .ToListAsync(ct);

        return sets.Count == 0 ? null : new LastRecordDto(entry.Date, sets);
    }

    private async Task<List<WorkoutEntryDto>> LoadEntriesAsync(int sessionId, CancellationToken ct) =>
        await db.WorkoutEntries
            .AsNoTracking()
            .Where(e => e.SessionId == sessionId)
            .OrderBy(e => e.OrderIndex)
            .Select(e => new WorkoutEntryDto(
                e.Id,
                e.SessionId,
                e.ExerciseId,
                e.ExerciseName,
                e.Target,
                e.OrderIndex,
                e.Sets.OrderBy(s => s.SetNumber)
                    .Select(s => new WorkoutSetDto(s.Id, s.EntryId, s.SetNumber, s.Weight, s.Reps, s.Completed))
                    .ToList()))
            .ToListAsync(ct);

    private async Task<WorkoutEntry> AddEntryEntityAsync(int sessionId, string exerciseId, string exerciseName, string target, CancellationToken ct)
    {
        var maxOrder = await db.WorkoutEntries
            .Where(e => e.SessionId == sessionId)
            .Select(e => (int?)e.OrderIndex)
            .MaxAsync(ct);
        var nextOrder = maxOrder is { } m ? m + 1 : 0;

        var entry = new WorkoutEntry
        {
            SessionId = sessionId,
            ExerciseId = exerciseId,
            ExerciseName = exerciseName,
            Target = target,
            OrderIndex = nextOrder,
        };
        db.WorkoutEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry;
    }

    private async Task<WorkoutSet> AddSetEntityAsync(int entryId, double weight, int reps, CancellationToken ct)
    {
        var maxNumber = await db.WorkoutSets
            .Where(s => s.EntryId == entryId)
            .Select(s => (int?)s.SetNumber)
            .MaxAsync(ct);
        var nextNumber = maxNumber is { } m ? m + 1 : 1;

        var set = new WorkoutSet
        {
            EntryId = entryId,
            SetNumber = nextNumber,
            Weight = weight,
            Reps = reps,
        };
        db.WorkoutSets.Add(set);
        await db.SaveChangesAsync(ct);
        return set;
    }

    private static readonly System.Linq.Expressions.Expression<Func<WorkoutSession, WorkoutSessionDto>> ProjectSession =
        s => new WorkoutSessionDto(s.Id, s.Date, s.Note, s.DurationSec);

    private static readonly System.Linq.Expressions.Expression<Func<WorkoutSet, WorkoutSetDto>> ProjectSetExpr =
        s => new WorkoutSetDto(s.Id, s.EntryId, s.SetNumber, s.Weight, s.Reps, s.Completed);

    private static WorkoutSetDto ProjectSetDto(WorkoutSet s) =>
        new(s.Id, s.EntryId, s.SetNumber, s.Weight, s.Reps, s.Completed);
}

public sealed record WorkoutSessionDto(int Id, DateOnly Date, string? Note, int DurationSec);

public sealed record WorkoutEntryDto(
    int Id,
    int SessionId,
    string ExerciseId,
    string ExerciseName,
    string Target,
    int OrderIndex,
    List<WorkoutSetDto> Sets);

public sealed record WorkoutSetDto(int Id, int EntryId, int SetNumber, double Weight, int Reps, bool Completed);

public sealed record LastRecordDto(DateOnly Date, List<WorkoutSetDto> Sets);
