using System.Text.Json;
using System.Text.RegularExpressions;
using GymHub.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace GymHub.Server.Stats;

/// <summary>
/// Read-only workout aggregations for the statistics tab and per-exercise
/// analysis (App #12/#13/#17). Category mapping and volume rules can't be
/// translated to SQL, so each method pulls the user's flat set rows for the
/// relevant date range and aggregates them in memory.
/// </summary>
public sealed partial class StatsService(GymHubDbContext db)
{
    /// <summary>Per-body-part volume/sets/reps for the current and previous period (week or month).</summary>
    public async Task<BodyPartVolumeResponse> GetBodyPartVolumeAsync(
        int userId, string period, DateOnly today, CancellationToken ct)
    {
        var isMonth = string.Equals(period, "month", StringComparison.OrdinalIgnoreCase);
        var (curStart, curEnd) = isMonth ? MonthRange(today, 0) : WeekRange(today, 0);
        var (prevStart, prevEnd) = isMonth ? MonthRange(today, -1) : WeekRange(today, -1);

        var rows = await LoadRowsAsync(userId, prevStart, curEnd, ct);

        var current = AggregateByCategory(rows.Where(r => r.Date >= curStart && r.Date <= curEnd));
        var previous = AggregateByCategory(rows.Where(r => r.Date >= prevStart && r.Date <= prevEnd));

        return new BodyPartVolumeResponse(
            isMonth ? "month" : "week",
            curStart, curEnd, prevStart, prevEnd,
            current, previous);
    }

    /// <summary>
    /// Per-body-part fatigue over the last 7 days. Each day's category volume is
    /// weighted by recency (today ×1, then ×<see cref="FatigueDecay"/> per day back),
    /// then scaled to 0–100 relative to the most-loaded category.
    /// </summary>
    public async Task<FatigueResponse> GetFatigueAsync(int userId, DateOnly today, CancellationToken ct)
    {
        var from = today.AddDays(-6);
        var rows = await LoadRowsAsync(userId, from, today, ct);

        var weighted = new Dictionary<string, double>();
        var volume = new Dictionary<string, double>();
        foreach (var r in rows)
        {
            var category = Category(r.Target);
            var v = Volume(r);
            var dayBack = today.DayNumber - r.Date.DayNumber; // 0 = today
            weighted[category] = weighted.GetValueOrDefault(category) + (v * Math.Pow(FatigueDecay, dayBack));
            volume[category] = volume.GetValueOrDefault(category) + v;
        }

        var peak = weighted.Values.DefaultIfEmpty(0).Max();
        var items = weighted
            .Select(kv => new CategoryFatigue(
                kv.Key,
                peak > 0 ? Math.Round(kv.Value / peak * 100, 1) : 0,
                Math.Round(volume.GetValueOrDefault(kv.Key), 1)))
            .OrderByDescending(i => i.Score)
            .ToList();

        return new FatigueResponse(from, today, items);
    }

    /// <summary>This week vs last week, compared weekday by weekday (volume/sets/reps) plus totals.</summary>
    public async Task<WeeklyTrendResponse> GetWeeklyTrendAsync(int userId, DateOnly today, CancellationToken ct)
    {
        var (thisStart, thisEnd) = WeekRange(today, 0);
        var (lastStart, _) = WeekRange(today, -1);

        var rows = await LoadRowsAsync(userId, lastStart, thisEnd, ct);

        var days = new List<WeekdayTrend>(7);
        for (var i = 0; i < 7; i++)
        {
            var thisDay = Totals(rows.Where(r => r.Date == thisStart.AddDays(i)));
            var lastDay = Totals(rows.Where(r => r.Date == lastStart.AddDays(i)));
            days.Add(new WeekdayTrend(i + 1, thisDay, lastDay)); // 1 = Monday
        }

        var thisTotal = Totals(rows.Where(r => r.Date >= thisStart && r.Date <= thisEnd));
        var lastTotal = Totals(rows.Where(r => r.Date >= lastStart && r.Date < thisStart));

        return new WeeklyTrendResponse(thisStart, lastStart, days, thisTotal, lastTotal);
    }

    /// <summary>
    /// Per-exercise analysis: estimated 1RM (Epley), top weight, best single-session
    /// volume, and the per-session history (oldest→newest) for the overload trend.
    /// </summary>
    public async Task<ExerciseAnalysisResponse> GetExerciseAnalysisAsync(
        int userId, string exerciseId, CancellationToken ct)
    {
        var rows = await db.WorkoutSets.AsNoTracking()
            .Where(s => s.Entry!.ExerciseId == exerciseId && s.Entry.Session!.UserId == userId)
            .Select(s => new { s.Entry!.Session!.Date, s.Weight, s.Reps })
            .ToListAsync(ct);

        var history = rows
            .GroupBy(r => r.Date)
            .OrderBy(g => g.Key)
            .Select(g => new ExercisePoint(
                g.Key,
                g.Max(r => r.Weight),
                g.Sum(r => r.Weight * r.Reps),
                Math.Round(g.Max(r => OneRepMax(r.Weight, r.Reps)), 1)))
            .ToList();

        return new ExerciseAnalysisResponse(
            exerciseId,
            history.Select(p => p.EstimatedOneRm).DefaultIfEmpty(0).Max(),
            history.Select(p => p.TopWeight).DefaultIfEmpty(0).Max(),
            history.Select(p => p.Volume).DefaultIfEmpty(0).Max(),
            history);
    }

    // ----- raw per-entry feeds (the app aggregates these client-side) -----

    /// <summary>(date, durationSec) for every session. Powers the recent-7-days duration view.</summary>
    public async Task<List<SessionDurationStat>> GetSessionDurationsAsync(int userId, CancellationToken ct) =>
        await db.WorkoutSessions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.Date)
            .Select(s => new SessionDurationStat(s.Date, s.DurationSec))
            .ToListAsync(ct);

    /// <summary>Per-entry stats with body part + secondary muscles (joined from the exercise catalog).</summary>
    public async Task<List<EntryStat>> GetEntryStatsAsync(int userId, CancellationToken ct)
    {
        var entries = await LoadEntriesWithSetsAsync(userId, ct);
        var meta = await LoadExerciseMetaAsync(entries.Select(e => e.ExerciseId), ct);

        return entries
            .Select(e =>
            {
                meta.TryGetValue(e.ExerciseId, out var m);
                return new EntryStat(
                    e.Date, e.ExerciseName, m.BodyPart ?? "", e.Target,
                    ParseMuscles(m.SecondaryMuscles),
                    Math.Round(EntryVolume(e), 1),
                    e.Sets.Count);
            })
            .ToList();
    }

    /// <summary>Per-entry top weight/volume/sets for the per-exercise growth view.</summary>
    public async Task<List<ExerciseProgressStat>> GetExerciseProgressAsync(int userId, CancellationToken ct)
    {
        var entries = await LoadEntriesWithSetsAsync(userId, ct);
        return entries
            .Select(e => new ExerciseProgressStat(
                e.Date, e.ExerciseId, e.ExerciseName, e.Target,
                e.Sets.Count == 0 ? 0 : e.Sets.Max(s => s.Weight),
                Math.Round(EntryVolume(e), 1),
                e.Sets.Count))
            .ToList();
    }

    // ----- helpers -----

    private async Task<List<EntryRow>> LoadEntriesWithSetsAsync(int userId, CancellationToken ct) =>
        await db.WorkoutEntries.AsNoTracking()
            .Where(e => e.Session!.UserId == userId)
            .Select(e => new EntryRow(
                e.Session!.Date, e.ExerciseId, e.ExerciseName, e.Target,
                e.Sets.Select(s => new SetWeightReps(s.Weight, s.Reps)).ToList()))
            .ToListAsync(ct);

    private async Task<Dictionary<string, (string? BodyPart, string? SecondaryMuscles)>> LoadExerciseMetaAsync(
        IEnumerable<string> exerciseIds, CancellationToken ct)
    {
        var ids = exerciseIds.Distinct().ToList();
        var rows = await db.Exercises.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.BodyPart, x.SecondaryMuscles })
            .ToListAsync(ct);
        return rows.ToDictionary(x => x.Id, x => ((string?)x.BodyPart, x.SecondaryMuscles));
    }

    private static double EntryVolume(EntryRow e) =>
        IsVolumeExcluded(e.ExerciseId, e.ExerciseName) ? 0 : e.Sets.Sum(s => s.Weight * s.Reps);

    /// <summary>Decodes a stored secondary-muscles value (JSON array, or newline-separated fallback).</summary>
    private static List<string> ParseMuscles(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(raw) ?? [];
        }
        catch (JsonException)
        {
            return raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }
    }

    private sealed record EntryRow(DateOnly Date, string ExerciseId, string ExerciseName, string Target, List<SetWeightReps> Sets);

    private sealed record SetWeightReps(double Weight, int Reps);


    /// <summary>Per-day exponential decay applied to volume when scoring fatigue.</summary>
    private const double FatigueDecay = 0.8;

    private async Task<List<SetRow>> LoadRowsAsync(int userId, DateOnly start, DateOnly end, CancellationToken ct) =>
        await db.WorkoutSets.AsNoTracking()
            .Where(s => s.Entry!.Session!.UserId == userId
                && s.Entry.Session.Date >= start
                && s.Entry.Session.Date <= end)
            .Select(s => new SetRow(
                s.Entry!.Session!.Date, s.Entry.ExerciseId, s.Entry.ExerciseName, s.Entry.Target, s.Weight, s.Reps))
            .ToListAsync(ct);

    private static List<CategoryVolume> AggregateByCategory(IEnumerable<SetRow> rows) =>
        rows.GroupBy(r => Category(r.Target))
            .Select(g => new CategoryVolume(
                g.Key,
                Math.Round(g.Sum(Volume), 1),
                g.Count(),
                g.Sum(r => r.Reps)))
            .OrderByDescending(c => c.Volume)
            .ToList();

    private static TrendTotals Totals(IEnumerable<SetRow> rows)
    {
        var list = rows.ToList();
        return new TrendTotals(Math.Round(list.Sum(Volume), 1), list.Count, list.Sum(r => r.Reps));
    }

    private static double OneRepMax(double weight, int reps) =>
        reps <= 0 ? weight : weight * (1 + (reps / 30.0));

    private static double Volume(SetRow r) =>
        IsVolumeExcluded(r.ExerciseId, r.ExerciseName) ? 0 : r.Weight * r.Reps;

    /// <summary>Week range [Monday, Sunday] of the week <paramref name="offset"/> weeks from <paramref name="today"/>.</summary>
    private static (DateOnly Start, DateOnly End) WeekRange(DateOnly today, int offset)
    {
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)).AddDays(offset * 7);
        return (monday, monday.AddDays(6));
    }

    /// <summary>Calendar-month range of the month <paramref name="offset"/> months from <paramref name="today"/>.</summary>
    private static (DateOnly Start, DateOnly End) MonthRange(DateOnly today, int offset)
    {
        var first = new DateOnly(today.Year, today.Month, 1).AddMonths(offset);
        return (first, first.AddMonths(1).AddDays(-1));
    }

    /// <summary>Maps an exercise <c>target</c> muscle to a coarse body category (mirrors the app's grouping).</summary>
    private static string Category(string target) => target.ToLowerInvariant() switch
    {
        "pectorals" => "가슴",
        "biceps" => "이두",
        "triceps" => "삼두",
        "deltoids" or "delts" => "어깨",
        "quads" or "hamstrings" or "glutes" or "calves"
            or "abductors" or "adductors" or "hip flexors" => "하체",
        "abs" or "obliques" => "복근",
        "lats" or "spine" or "trapezius" or "traps"
            or "rhomboids" or "upper back" or "levator scapulae" => "등",
        _ => "기타",
    };

    /// <summary>
    /// Assisted dips/pull-ups don't represent lifted load, so they're excluded from
    /// volume (mirrors the app's <c>isVolumeExcludedExercise</c>).
    /// </summary>
    private static bool IsVolumeExcluded(string exerciseId, string exerciseName)
    {
        var normalized = NonWord().Replace($"{exerciseId} {exerciseName}".ToLowerInvariant(), " ").Trim();
        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var hasAssist = normalized.Contains("assisted") || normalized.Contains("assistance")
            || normalized.Contains("어시스턴스") || normalized.Contains("보조");
        var hasDip = tokens.Contains("dip") || tokens.Contains("dips") || normalized.Contains("딥");
        var hasPullUp = normalized.Contains("pull up") || normalized.Contains("pullup") || normalized.Contains("풀업");
        return hasAssist && (hasDip || hasPullUp);
    }

    [GeneratedRegex("[^a-z0-9가-힣]+")]
    private static partial Regex NonWord();

    private sealed record SetRow(DateOnly Date, string ExerciseId, string ExerciseName, string Target, double Weight, int Reps);
}

public sealed record BodyPartVolumeResponse(
    string Period,
    DateOnly CurrentStart,
    DateOnly CurrentEnd,
    DateOnly PreviousStart,
    DateOnly PreviousEnd,
    List<CategoryVolume> Current,
    List<CategoryVolume> Previous);

public sealed record CategoryVolume(string Category, double Volume, int Sets, int Reps);

public sealed record FatigueResponse(DateOnly From, DateOnly To, List<CategoryFatigue> Items);

public sealed record CategoryFatigue(string Category, double Score, double Volume);

public sealed record WeeklyTrendResponse(
    DateOnly ThisWeekStart,
    DateOnly LastWeekStart,
    List<WeekdayTrend> Days,
    TrendTotals ThisWeek,
    TrendTotals LastWeek);

public sealed record WeekdayTrend(int Weekday, TrendTotals ThisWeek, TrendTotals LastWeek);

public sealed record TrendTotals(double Volume, int Sets, int Reps);

public sealed record ExerciseAnalysisResponse(
    string ExerciseId,
    double EstimatedOneRm,
    double TopWeight,
    double MaxVolume,
    List<ExercisePoint> History);

public sealed record ExercisePoint(DateOnly Date, double TopWeight, double Volume, double EstimatedOneRm);

public sealed record SessionDurationStat(DateOnly Date, int DurationSec);

public sealed record EntryStat(
    DateOnly Date,
    string ExerciseName,
    string BodyPart,
    string Target,
    List<string> SecondaryMuscles,
    double Volume,
    int Sets);

public sealed record ExerciseProgressStat(
    DateOnly Date,
    string ExerciseId,
    string ExerciseName,
    string Target,
    double TopWeight,
    double Volume,
    int Sets);
