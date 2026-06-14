using GymHub.Server.Data;
using GymHub.Server.Stats;
using Microsoft.EntityFrameworkCore;

namespace GymHub.Server.Tests;

public sealed class StatsServiceTests
{
    private const int UserId = 1;
    private const int OtherUserId = 2;
    private static readonly DateOnly Today = new(2026, 6, 14); // Sunday

    [Fact]
    public async Task BodyPartVolumeSplitsCurrentAndPreviousWeekByCategoryAndScopesToUser()
    {
        await using var db = NewDb();
        // 이번 주(6/8~6/14): 가슴 60kg, 등 100kg. 지난 주(6/1~6/7): 가슴 40kg.
        Seed(db, sessionId: 1, UserId, new DateOnly(2026, 6, 9), "ex1", "Bench", "pectorals", (50, 1), (10, 1));
        Seed(db, sessionId: 2, UserId, new DateOnly(2026, 6, 10), "ex2", "Row", "lats", (50, 2));
        Seed(db, sessionId: 3, UserId, new DateOnly(2026, 6, 3), "ex1", "Bench", "pectorals", (40, 1));
        Seed(db, sessionId: 4, OtherUserId, new DateOnly(2026, 6, 9), "ex1", "Bench", "pectorals", (999, 1));
        await db.SaveChangesAsync();
        var service = new StatsService(db);

        var res = await service.GetBodyPartVolumeAsync(UserId, "week", Today, default);

        Assert.Equal("week", res.Period);
        Assert.Equal(new DateOnly(2026, 6, 8), res.CurrentStart);
        Assert.Equal(new DateOnly(2026, 6, 14), res.CurrentEnd);
        Assert.Equal(100, res.Current.Single(c => c.Category == "등").Volume);
        Assert.Equal(60, res.Current.Single(c => c.Category == "가슴").Volume);
        Assert.Equal(40, res.Previous.Single(c => c.Category == "가슴").Volume);
        Assert.DoesNotContain(res.Current, c => c.Volume >= 999); // 타 유저 제외
    }

    [Fact]
    public async Task FatigueWeightsRecentDaysMoreAndScalesToHundred()
    {
        await using var db = NewDb();
        // 가슴: 오늘 100kg(가중치 1). 등: 6일 전 100kg(가중치 0.8^6).
        Seed(db, sessionId: 1, UserId, Today, "ex1", "Bench", "pectorals", (100, 1));
        Seed(db, sessionId: 2, UserId, Today.AddDays(-6), "ex2", "Row", "lats", (100, 1));
        await db.SaveChangesAsync();
        var service = new StatsService(db);

        var res = await service.GetFatigueAsync(UserId, Today, default);

        var chest = res.Items.Single(i => i.Category == "가슴");
        var back = res.Items.Single(i => i.Category == "등");
        Assert.Equal(100, chest.Score); // 최대 부하 → 100
        Assert.True(back.Score < chest.Score); // 오래된 부하는 감쇠
        Assert.Equal(100, chest.Volume);
    }

    [Fact]
    public async Task FatigueExcludesDataOlderThanSevenDays()
    {
        await using var db = NewDb();
        Seed(db, sessionId: 1, UserId, Today.AddDays(-7), "ex1", "Bench", "pectorals", (100, 1));
        await db.SaveChangesAsync();
        var service = new StatsService(db);

        var res = await service.GetFatigueAsync(UserId, Today, default);

        Assert.Empty(res.Items);
    }

    [Fact]
    public async Task WeeklyTrendComparesSameWeekdayAndTotals()
    {
        await using var db = NewDb();
        // 이번 주 월(6/8) 50kg, 지난 주 월(6/1) 30kg.
        Seed(db, sessionId: 1, UserId, new DateOnly(2026, 6, 8), "ex1", "Bench", "pectorals", (50, 1));
        Seed(db, sessionId: 2, UserId, new DateOnly(2026, 6, 1), "ex1", "Bench", "pectorals", (30, 1));
        await db.SaveChangesAsync();
        var service = new StatsService(db);

        var res = await service.GetWeeklyTrendAsync(UserId, Today, default);

        Assert.Equal(7, res.Days.Count);
        var monday = res.Days.Single(d => d.Weekday == 1);
        Assert.Equal(50, monday.ThisWeek.Volume);
        Assert.Equal(30, monday.LastWeek.Volume);
        Assert.Equal(50, res.ThisWeek.Volume);
        Assert.Equal(30, res.LastWeek.Volume);
    }

    [Fact]
    public async Task ExerciseAnalysisComputesOneRmTopWeightAndHistory()
    {
        await using var db = NewDb();
        // 6/1: 100kg×5 (1RM = 100*(1+5/30) ≈ 116.7), 6/8: 110kg×1 (top weight 110).
        Seed(db, sessionId: 1, UserId, new DateOnly(2026, 6, 1), "ex1", "Squat", "quads", (100, 5));
        Seed(db, sessionId: 2, UserId, new DateOnly(2026, 6, 8), "ex1", "Squat", "quads", (110, 1), (90, 5));
        await db.SaveChangesAsync();
        var service = new StatsService(db);

        var res = await service.GetExerciseAnalysisAsync(UserId, "ex1", default);

        Assert.Equal(2, res.History.Count);
        Assert.Equal(new DateOnly(2026, 6, 1), res.History[0].Date); // 오래된 순
        Assert.Equal(110, res.TopWeight);
        Assert.Equal(560, res.MaxVolume); // 6/8: 110*1 + 90*5 = 560
        Assert.Equal(116.7, res.EstimatedOneRm); // 6/1 세트가 최고 1RM
    }

    [Fact]
    public async Task AssistedPullUpIsExcludedFromVolume()
    {
        await using var db = NewDb();
        Seed(db, sessionId: 1, UserId, Today, "0123", "Assisted Pull-up", "lats", (40, 10));
        await db.SaveChangesAsync();
        var service = new StatsService(db);

        var res = await service.GetBodyPartVolumeAsync(UserId, "week", Today, default);

        Assert.Equal(0, res.Current.Single(c => c.Category == "등").Volume);
    }

    private static void Seed(
        GymHubDbContext db, int sessionId, int userId, DateOnly date,
        string exerciseId, string exerciseName, string target, params (double Weight, int Reps)[] sets)
    {
        db.WorkoutSessions.Add(new WorkoutSession { Id = sessionId, UserId = userId, Date = date });
        var entryId = sessionId * 100;
        db.WorkoutEntries.Add(new WorkoutEntry
        {
            Id = entryId,
            SessionId = sessionId,
            ExerciseId = exerciseId,
            ExerciseName = exerciseName,
            Target = target,
            OrderIndex = 0,
        });
        for (var i = 0; i < sets.Length; i++)
        {
            db.WorkoutSets.Add(new WorkoutSet
            {
                Id = entryId + i + 1,
                EntryId = entryId,
                SetNumber = i + 1,
                Weight = sets[i].Weight,
                Reps = sets[i].Reps,
            });
        }
    }

    private static GymHubDbContext NewDb() =>
        new(new DbContextOptionsBuilder<GymHubDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
