using System.Linq.Expressions;
using GymHub.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace GymHub.Server.Exercises;

/// <summary>
/// Read access to the public exercise catalog plus Korean-name overrides.
/// The effective Korean name is resolved at query time via a left join on
/// <see cref="ExerciseNameOverride"/> so the seeded default (<c>exercises.name_ko</c>)
/// is never mutated and clearing an override restores it automatically.
/// </summary>
public sealed class ExerciseService(GymHubDbContext db)
{
    public async Task<ExercisePage> QueryAsync(ExerciseQuery query, CancellationToken ct)
    {
        var effective = WithEffectiveName();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            effective = effective.Where(x =>
                x.NameKo.ToLower().Contains(term) ||
                x.Exercise.Name.ToLower().Contains(term) ||
                x.Exercise.BodyPart.ToLower().Contains(term) ||
                x.Exercise.BodyPartKo.ToLower().Contains(term) ||
                x.Exercise.Target.ToLower().Contains(term) ||
                x.Exercise.TargetKo.ToLower().Contains(term) ||
                x.Exercise.Equipment.ToLower().Contains(term) ||
                x.Exercise.EquipmentKo.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(query.BodyPart))
        {
            effective = effective.Where(x => x.Exercise.BodyPartKo == query.BodyPart);
        }

        if (!string.IsNullOrWhiteSpace(query.Equipment))
        {
            effective = effective.Where(x => x.Exercise.Equipment == query.Equipment);
        }

        if (!string.IsNullOrWhiteSpace(query.Target))
        {
            effective = effective.Where(x => x.Exercise.Target == query.Target);
        }

        var total = await effective.CountAsync(ct);

        var items = await effective
            .OrderBy(x => x.NameKo)
            .ThenBy(x => x.Exercise.Name)
            .Skip(query.Offset)
            .Take(query.Limit)
            .Select(ProjectDto)
            .ToListAsync(ct);

        return new ExercisePage(items, query.Limit, query.Offset, total, query.Offset + items.Count < total);
    }

    public async Task<ExerciseDto?> GetByIdAsync(string id, CancellationToken ct) =>
        await WithEffectiveName()
            .Where(x => x.Exercise.Id == id)
            .Select(ProjectDto)
            .FirstOrDefaultAsync(ct);

    public async Task<Dictionary<string, string>> GetNameOverridesAsync(CancellationToken ct) =>
        await db.ExerciseNameOverrides
            .AsNoTracking()
            .ToDictionaryAsync(o => o.ExerciseId, o => o.NameKo, ct);

    /// <summary>
    /// Sets (or, when <paramref name="nameKo"/> is null/blank, clears) the Korean-name
    /// override for an exercise. Returns false when the exercise does not exist.
    /// </summary>
    public async Task<bool> SetNameOverrideAsync(string id, string? nameKo, CancellationToken ct)
    {
        if (!await db.Exercises.AnyAsync(e => e.Id == id, ct))
        {
            return false;
        }

        var existing = await db.ExerciseNameOverrides.FirstOrDefaultAsync(o => o.ExerciseId == id, ct);
        var trimmed = nameKo?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            if (existing is not null)
            {
                db.ExerciseNameOverrides.Remove(existing);
            }
        }
        else if (existing is null)
        {
            db.ExerciseNameOverrides.Add(new ExerciseNameOverride { ExerciseId = id, NameKo = trimmed });
        }
        else
        {
            existing.NameKo = trimmed;
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    private IQueryable<ExerciseRow> WithEffectiveName() =>
        from e in db.Exercises.AsNoTracking()
        join o in db.ExerciseNameOverrides on e.Id equals o.ExerciseId into overrides
        from o in overrides.DefaultIfEmpty()
        select new ExerciseRow { Exercise = e, NameKo = o == null ? e.NameKo : o.NameKo };

    private static readonly Expression<Func<ExerciseRow, ExerciseDto>> ProjectDto = x => new ExerciseDto(
        x.Exercise.Id,
        x.Exercise.Name,
        x.NameKo,
        x.Exercise.BodyPart,
        x.Exercise.BodyPartKo,
        x.Exercise.Target,
        x.Exercise.TargetKo,
        x.Exercise.Equipment,
        x.Exercise.EquipmentKo,
        x.Exercise.GifUrl,
        x.Exercise.SecondaryMuscles,
        x.Exercise.Instructions,
        x.Exercise.Description);

    private sealed class ExerciseRow
    {
        public required Exercise Exercise { get; init; }
        public required string NameKo { get; init; }
    }
}

public sealed record ExerciseQuery(
    string? Search,
    string? BodyPart,
    string? Target,
    string? Equipment,
    int Limit,
    int Offset);

public sealed record ExercisePage(
    IReadOnlyList<ExerciseDto> Items,
    int Limit,
    int Offset,
    int Total,
    bool HasMore);

public sealed record ExerciseDto(
    string Id,
    string Name,
    string NameKo,
    string BodyPart,
    string BodyPartKo,
    string Target,
    string TargetKo,
    string Equipment,
    string EquipmentKo,
    string GifUrl,
    string? SecondaryMuscles,
    string? Instructions,
    string? Description);
