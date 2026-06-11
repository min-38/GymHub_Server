using GymHub.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace GymHub.Server.Routines;

/// <summary>
/// CRUD for the authenticated user's workout routines (templates).
/// All reads and writes are scoped to <c>userId</c> (routines own that
/// foreign key; routine exercises are reached through their routine).
/// </summary>
public sealed class RoutineService(GymHubDbContext db)
{
    public async Task<List<RoutineDto>> GetAllAsync(int userId, CancellationToken ct) =>
        await db.Routines
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.OrderIndex)
            .ThenByDescending(r => r.Id)
            .Select(ProjectRoutine)
            .ToListAsync(ct);

    public async Task<RoutineDto?> GetByIdAsync(int userId, int routineId, CancellationToken ct) =>
        await db.Routines
            .AsNoTracking()
            .Where(r => r.Id == routineId && r.UserId == userId)
            .Select(ProjectRoutine)
            .FirstOrDefaultAsync(ct);

    /// <summary>Creates a new routine. Returns the created routine.</summary>
    public async Task<RoutineDto> CreateAsync(int userId, string name, string? note, CancellationToken ct)
    {
        var routine = await CreateRoutineEntityAsync(userId, name, note, ct);
        return new RoutineDto(routine.Id, routine.Name, routine.Note, routine.OrderIndex, []);
    }

    /// <summary>Creates a routine named "untitled", or "untitled-1", "untitled-2"… if taken (per user).</summary>
    public async Task<RoutineDto> CreateUntitledAsync(int userId, CancellationToken ct)
    {
        var names = await db.Routines
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .Select(r => r.Name)
            .ToListAsync(ct);
        var taken = names.ToHashSet();

        var name = "untitled";
        if (taken.Contains(name))
        {
            var n = 1;
            while (taken.Contains($"untitled-{n}"))
            {
                n++;
            }
            name = $"untitled-{n}";
        }

        return await CreateAsync(userId, name, null, ct);
    }

    /// <summary>Renames a routine and updates its note. Returns false if not found/owned.</summary>
    public async Task<bool> RenameAsync(int userId, int routineId, string name, string? note, CancellationToken ct)
    {
        var routine = await db.Routines.FirstOrDefaultAsync(r => r.Id == routineId && r.UserId == userId, ct);
        if (routine is null)
        {
            return false;
        }

        routine.Name = name;
        routine.Note = note;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Deletes a routine and (via FK cascade) its exercises. Returns false if not found/owned.</summary>
    public async Task<bool> DeleteAsync(int userId, int routineId, CancellationToken ct)
    {
        var routine = await db.Routines.FirstOrDefaultAsync(r => r.Id == routineId && r.UserId == userId, ct);
        if (routine is null)
        {
            return false;
        }

        db.Routines.Remove(routine);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Adds an exercise to the routine with default 3 sets × 10 reps × 0 weight.
    /// Returns null if the routine does not exist or is not owned by the user.
    /// </summary>
    public async Task<RoutineExerciseDto?> AddExerciseAsync(
        int userId, int routineId, string exerciseId, string exerciseName, string target, CancellationToken ct)
    {
        var routineOwned = await db.Routines.AnyAsync(r => r.Id == routineId && r.UserId == userId, ct);
        if (!routineOwned)
        {
            return null;
        }

        var routineExercise = await AddExerciseEntityAsync(
            routineId, exerciseId, exerciseName, target, defaultSets: 3, defaultReps: 10, defaultWeight: 0, ct);
        return ProjectExerciseDto(routineExercise);
    }

    /// <summary>
    /// Saves the user's workout session (by id) as a new routine. Each exercise's defaults are
    /// derived from its recorded sets: set count, and reps/weight from the first set with any value.
    /// Returns null if the session does not exist or is not owned by the user.
    /// </summary>
    public async Task<RoutineDto?> CreateFromSessionAsync(int userId, string name, int sessionId, CancellationToken ct)
    {
        var sessionOwned = await db.WorkoutSessions.AnyAsync(s => s.Id == sessionId && s.UserId == userId, ct);
        if (!sessionOwned)
        {
            return null;
        }

        var entries = await db.WorkoutEntries
            .AsNoTracking()
            .Where(e => e.SessionId == sessionId)
            .OrderBy(e => e.OrderIndex)
            .Select(e => new
            {
                e.ExerciseId,
                e.ExerciseName,
                e.Target,
                Sets = e.Sets.OrderBy(s => s.SetNumber)
                    .Select(s => new { s.Weight, s.Reps })
                    .ToList(),
            })
            .ToListAsync(ct);

        var routine = await CreateRoutineEntityAsync(userId, name, null, ct);

        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var baseSet = e.Sets.FirstOrDefault(s => s.Weight > 0 || s.Reps > 0) ?? e.Sets.FirstOrDefault();
            db.RoutineExercises.Add(new RoutineExercise
            {
                RoutineId = routine.Id,
                ExerciseId = e.ExerciseId,
                ExerciseName = e.ExerciseName,
                Target = e.Target,
                OrderIndex = i,
                DefaultSets = e.Sets.Count == 0 ? 1 : e.Sets.Count,
                DefaultReps = baseSet?.Reps ?? 0,
                DefaultWeight = baseSet?.Weight ?? 0,
            });
        }

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(userId, routine.Id, ct);
    }

    /// <summary>Updates a routine exercise's default sets/reps/weight. Returns false if not found/owned.</summary>
    public async Task<bool> UpdateExerciseDefaultsAsync(
        int userId, int routineExerciseId, int sets, int reps, double weight, CancellationToken ct)
    {
        var routineExercise = await db.RoutineExercises
            .FirstOrDefaultAsync(e => e.Id == routineExerciseId && e.Routine!.UserId == userId, ct);
        if (routineExercise is null)
        {
            return false;
        }

        routineExercise.DefaultSets = sets;
        routineExercise.DefaultReps = reps;
        routineExercise.DefaultWeight = weight;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RemoveExerciseAsync(int userId, int routineExerciseId, CancellationToken ct)
    {
        var routineExercise = await db.RoutineExercises
            .FirstOrDefaultAsync(e => e.Id == routineExerciseId && e.Routine!.UserId == userId, ct);
        if (routineExercise is null)
        {
            return false;
        }

        db.RoutineExercises.Remove(routineExercise);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Re-numbers exercise order_index to match the given order. Returns false if not found/owned.</summary>
    public async Task<bool> ReorderExercisesAsync(int userId, int routineId, List<int> orderedExerciseIds, CancellationToken ct)
    {
        var routineOwned = await db.Routines.AnyAsync(r => r.Id == routineId && r.UserId == userId, ct);
        if (!routineOwned)
        {
            return false;
        }

        var exercises = await db.RoutineExercises
            .Where(e => e.RoutineId == routineId && orderedExerciseIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, ct);

        for (var i = 0; i < orderedExerciseIds.Count; i++)
        {
            if (exercises.TryGetValue(orderedExerciseIds[i], out var exercise))
            {
                exercise.OrderIndex = i;
            }
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<Routine> CreateRoutineEntityAsync(int userId, string name, string? note, CancellationToken ct)
    {
        var maxOrder = await db.Routines
            .Where(r => r.UserId == userId)
            .Select(r => (int?)r.OrderIndex)
            .MaxAsync(ct);
        var nextOrder = maxOrder is { } m ? m + 1 : 0;

        var routine = new Routine { UserId = userId, Name = name, Note = note, OrderIndex = nextOrder };
        db.Routines.Add(routine);
        await db.SaveChangesAsync(ct);
        return routine;
    }

    private async Task<RoutineExercise> AddExerciseEntityAsync(
        int routineId, string exerciseId, string exerciseName, string target,
        int defaultSets, int defaultReps, double defaultWeight, CancellationToken ct)
    {
        var maxOrder = await db.RoutineExercises
            .Where(e => e.RoutineId == routineId)
            .Select(e => (int?)e.OrderIndex)
            .MaxAsync(ct);
        var nextOrder = maxOrder is { } m ? m + 1 : 0;

        var routineExercise = new RoutineExercise
        {
            RoutineId = routineId,
            ExerciseId = exerciseId,
            ExerciseName = exerciseName,
            Target = target,
            OrderIndex = nextOrder,
            DefaultSets = defaultSets,
            DefaultReps = defaultReps,
            DefaultWeight = defaultWeight,
        };
        db.RoutineExercises.Add(routineExercise);
        await db.SaveChangesAsync(ct);
        return routineExercise;
    }

    private static readonly System.Linq.Expressions.Expression<Func<Routine, RoutineDto>> ProjectRoutine =
        r => new RoutineDto(
            r.Id,
            r.Name,
            r.Note,
            r.OrderIndex,
            r.Exercises.OrderBy(e => e.OrderIndex)
                .Select(e => new RoutineExerciseDto(
                    e.Id, e.RoutineId, e.ExerciseId, e.ExerciseName, e.Target,
                    e.OrderIndex, e.DefaultSets, e.DefaultReps, e.DefaultWeight))
                .ToList());

    private static RoutineExerciseDto ProjectExerciseDto(RoutineExercise e) =>
        new(e.Id, e.RoutineId, e.ExerciseId, e.ExerciseName, e.Target,
            e.OrderIndex, e.DefaultSets, e.DefaultReps, e.DefaultWeight);
}

public sealed record RoutineDto(
    int Id, string Name, string? Note, int OrderIndex, List<RoutineExerciseDto> Exercises);

public sealed record RoutineExerciseDto(
    int Id,
    int RoutineId,
    string ExerciseId,
    string ExerciseName,
    string Target,
    int OrderIndex,
    int DefaultSets,
    int DefaultReps,
    double DefaultWeight);
