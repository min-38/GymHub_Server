using GymHub.Server.Data;
using GymHub.Server.Workouts;
using Microsoft.EntityFrameworkCore;

namespace GymHub.Server.Tests;

public sealed class WorkoutServiceTests
{
    private const int UserId = 1;
    private const int OtherUserId = 2;

    [Fact]
    public async Task SessionsAreOrderedByDateThenIdDescendingAndScopedToUser()
    {
        await using var db = NewDb();
        db.WorkoutSessions.AddRange(
            new WorkoutSession { Id = 1, UserId = UserId, Date = new DateOnly(2026, 6, 1) },
            new WorkoutSession { Id = 2, UserId = UserId, Date = new DateOnly(2026, 6, 3) },
            new WorkoutSession { Id = 3, UserId = UserId, Date = new DateOnly(2026, 6, 3) },
            new WorkoutSession { Id = 4, UserId = OtherUserId, Date = new DateOnly(2026, 6, 5) });
        await db.SaveChangesAsync();
        var service = new WorkoutService(db);

        var sessions = await service.GetSessionsAsync(UserId, default);

        Assert.Equal([3, 2, 1], sessions.Select(s => s.Id));
    }

    [Fact]
    public async Task EnsureSessionReturnsExistingOrCreatesNew()
    {
        await using var db = NewDb();
        var date = new DateOnly(2026, 6, 1);
        db.WorkoutSessions.Add(new WorkoutSession { Id = 1, UserId = UserId, Date = date, Note = "기존" });
        await db.SaveChangesAsync();
        var service = new WorkoutService(db);

        var existing = await service.EnsureSessionAsync(UserId, date, default);
        Assert.Equal(1, existing.Id);
        Assert.Equal("기존", existing.Note);

        var created = await service.EnsureSessionAsync(UserId, new DateOnly(2026, 6, 2), default);
        Assert.True(created.Id > 0);
        Assert.Equal(0, created.DurationSec);
        Assert.Equal(2, await db.WorkoutSessions.CountAsync());
    }

    [Fact]
    public async Task UpdateSessionSetsNoteAndDuration_AndRejectsOtherUsers()
    {
        await using var db = NewDb();
        db.WorkoutSessions.Add(new WorkoutSession { Id = 1, UserId = UserId, Date = new DateOnly(2026, 6, 1) });
        await db.SaveChangesAsync();
        var service = new WorkoutService(db);

        Assert.True(await service.UpdateSessionAsync(UserId, 1, "메모", 120, default));
        var session = await db.WorkoutSessions.FindAsync(1);
        Assert.Equal("메모", session!.Note);
        Assert.Equal(120, session.DurationSec);

        Assert.False(await service.UpdateSessionAsync(OtherUserId, 1, "도용", null, default));
    }

    [Fact]
    public async Task DeleteSessionCascadesEntriesAndSets()
    {
        await using var db = NewDb();
        var session = new WorkoutSession { Id = 1, UserId = UserId, Date = new DateOnly(2026, 6, 1) };
        db.WorkoutSessions.Add(session);
        db.WorkoutEntries.Add(new WorkoutEntry { Id = 1, SessionId = 1, ExerciseId = "ex1", ExerciseName = "Bench", Target = "chest", OrderIndex = 0 });
        db.WorkoutSets.Add(new WorkoutSet { Id = 1, EntryId = 1, SetNumber = 1, Weight = 60, Reps = 10 });
        await db.SaveChangesAsync();
        var service = new WorkoutService(db);

        Assert.True(await service.DeleteSessionAsync(UserId, 1, default));
        Assert.Empty(await db.WorkoutSessions.ToListAsync());
        Assert.Empty(await db.WorkoutEntries.ToListAsync());
        Assert.Empty(await db.WorkoutSets.ToListAsync());
    }

    [Fact]
    public async Task DeleteSessionReturnsFalseForUnknownOrUnowned()
    {
        await using var db = NewDb();
        db.WorkoutSessions.Add(new WorkoutSession { Id = 1, UserId = OtherUserId, Date = new DateOnly(2026, 6, 1) });
        await db.SaveChangesAsync();
        var service = new WorkoutService(db);

        Assert.False(await service.DeleteSessionAsync(UserId, 1, default));
        Assert.False(await service.DeleteSessionAsync(UserId, 999, default));
    }

    [Fact]
    public async Task EntriesOfDateReturnsEmptyWhenNoSession()
    {
        await using var db = NewDb();
        var service = new WorkoutService(db);

        Assert.Empty(await service.GetEntriesOfDateAsync(UserId, new DateOnly(2026, 6, 1), default));
    }

    [Fact]
    public async Task WorkoutDatesOnlyIncludesDatesWithEntries()
    {
        await using var db = NewDb();
        db.WorkoutSessions.AddRange(
            new WorkoutSession { Id = 1, UserId = UserId, Date = new DateOnly(2026, 6, 1) },
            new WorkoutSession { Id = 2, UserId = UserId, Date = new DateOnly(2026, 6, 2) });
        db.WorkoutEntries.Add(new WorkoutEntry { Id = 1, SessionId = 1, ExerciseId = "ex1", ExerciseName = "Bench", Target = "chest", OrderIndex = 0 });
        await db.SaveChangesAsync();
        var service = new WorkoutService(db);

        var dates = await service.GetWorkoutDatesAsync(UserId, default);

        Assert.Equal([new DateOnly(2026, 6, 1)], dates);
    }

    [Fact]
    public async Task AddExerciseWithNoPriorRecordCreatesOneEmptySet()
    {
        await using var db = NewDb();
        db.WorkoutSessions.Add(new WorkoutSession { Id = 1, UserId = UserId, Date = new DateOnly(2026, 6, 1) });
        await db.SaveChangesAsync();
        var service = new WorkoutService(db);

        var entry = await service.AddExerciseAsync(UserId, 1, "ex1", "Bench Press", "chest", default);

        Assert.NotNull(entry);
        Assert.Equal(0, entry!.OrderIndex);
        var set = Assert.Single(entry.Sets);
        Assert.Equal(1, set.SetNumber);
        Assert.Equal(0, set.Weight);
        Assert.Equal(0, set.Reps);
    }

    [Fact]
    public async Task AddExercisePrefillsFromMostRecentOtherSessionRecord()
    {
        await using var db = NewDb();
        // 과거 기록 (다른 세션)
        db.WorkoutSessions.Add(new WorkoutSession { Id = 1, UserId = UserId, Date = new DateOnly(2026, 5, 1) });
        db.WorkoutEntries.Add(new WorkoutEntry { Id = 1, SessionId = 1, ExerciseId = "ex1", ExerciseName = "Bench Press", Target = "chest", OrderIndex = 0 });
        db.WorkoutSets.AddRange(
            new WorkoutSet { Id = 1, EntryId = 1, SetNumber = 1, Weight = 60, Reps = 8 },
            new WorkoutSet { Id = 2, EntryId = 1, SetNumber = 2, Weight = 65, Reps = 6 });
        // 오늘 세션
        db.WorkoutSessions.Add(new WorkoutSession { Id = 2, UserId = UserId, Date = new DateOnly(2026, 6, 1) });
        await db.SaveChangesAsync();
        var service = new WorkoutService(db);

        var entry = await service.AddExerciseAsync(UserId, 2, "ex1", "Bench Press", "chest", default);

        Assert.NotNull(entry);
        Assert.Equal(2, entry!.Sets.Count);
        Assert.Equal((60.0, 8), (entry.Sets[0].Weight, entry.Sets[0].Reps));
        Assert.Equal((65.0, 6), (entry.Sets[1].Weight, entry.Sets[1].Reps));
    }

    [Fact]
    public async Task AddExerciseReturnsNullForUnownedSession()
    {
        await using var db = NewDb();
        db.WorkoutSessions.Add(new WorkoutSession { Id = 1, UserId = OtherUserId, Date = new DateOnly(2026, 6, 1) });
        await db.SaveChangesAsync();
        var service = new WorkoutService(db);

        Assert.Null(await service.AddExerciseAsync(UserId, 1, "ex1", "Bench Press", "chest", default));
    }

    [Fact]
    public async Task DeleteEntryCascadesSetsAndRespectsOwnership()
    {
        await using var db = NewDb();
        db.WorkoutSessions.Add(new WorkoutSession { Id = 1, UserId = UserId, Date = new DateOnly(2026, 6, 1) });
        db.WorkoutEntries.Add(new WorkoutEntry { Id = 1, SessionId = 1, ExerciseId = "ex1", ExerciseName = "Bench", Target = "chest", OrderIndex = 0 });
        db.WorkoutSets.Add(new WorkoutSet { Id = 1, EntryId = 1, SetNumber = 1, Weight = 60, Reps = 10 });
        await db.SaveChangesAsync();
        var service = new WorkoutService(db);

        Assert.False(await service.DeleteEntryAsync(OtherUserId, 1, default));
        Assert.True(await service.DeleteEntryAsync(UserId, 1, default));
        Assert.Empty(await db.WorkoutEntries.ToListAsync());
        Assert.Empty(await db.WorkoutSets.ToListAsync());
    }

    [Fact]
    public async Task ChangeEntryExerciseSwapsIdentityAndKeepsSetsAndOrder()
    {
        await using var db = NewDb();
        db.WorkoutSessions.Add(new WorkoutSession { Id = 1, UserId = UserId, Date = new DateOnly(2026, 6, 1) });
        db.WorkoutEntries.Add(new WorkoutEntry { Id = 1, SessionId = 1, ExerciseId = "ex1", ExerciseName = "Bench", Target = "chest", OrderIndex = 2 });
        db.WorkoutSets.AddRange(
            new WorkoutSet { Id = 1, EntryId = 1, SetNumber = 1, Weight = 60, Reps = 10 },
            new WorkoutSet { Id = 2, EntryId = 1, SetNumber = 2, Weight = 65, Reps = 8 });
        await db.SaveChangesAsync();
        var service = new WorkoutService(db);

        Assert.Null(await service.ChangeEntryExerciseAsync(OtherUserId, 1, "ex2", "Row", "back", default));

        var updated = await service.ChangeEntryExerciseAsync(UserId, 1, "ex2", "Barbell Row", "back", default);
        Assert.NotNull(updated);
        Assert.Equal("ex2", updated!.ExerciseId);
        Assert.Equal("Barbell Row", updated.ExerciseName);
        Assert.Equal("back", updated.Target);
        Assert.Equal(2, updated.OrderIndex);
        Assert.Equal(2, updated.Sets.Count);
        Assert.Equal(60, updated.Sets[0].Weight);
        Assert.Equal(8, updated.Sets[1].Reps);
    }

    [Fact]
    public async Task SetEntryRestStoresAndClearsAndRespectsOwnership()
    {
        await using var db = NewDb();
        db.WorkoutSessions.Add(new WorkoutSession { Id = 1, UserId = UserId, Date = new DateOnly(2026, 6, 1) });
        db.WorkoutEntries.Add(new WorkoutEntry { Id = 1, SessionId = 1, ExerciseId = "ex1", ExerciseName = "Bench", Target = "chest", OrderIndex = 0 });
        await db.SaveChangesAsync();
        var service = new WorkoutService(db);

        Assert.False(await service.SetEntryRestAsync(OtherUserId, 1, 90, default));

        Assert.True(await service.SetEntryRestAsync(UserId, 1, 90, default));
        Assert.Equal(90, (await db.WorkoutEntries.FindAsync(1))!.RestSec);
        var loaded = await service.GetEntriesOfDateAsync(UserId, new DateOnly(2026, 6, 1), default);
        Assert.Equal(90, loaded.Single().RestSec);

        // null clears it back to "use global default"
        Assert.True(await service.SetEntryRestAsync(UserId, 1, null, default));
        Assert.Null((await db.WorkoutEntries.FindAsync(1))!.RestSec);
    }

    [Fact]
    public async Task ReorderEntriesUpdatesOrderIndex()
    {
        await using var db = NewDb();
        db.WorkoutSessions.Add(new WorkoutSession { Id = 1, UserId = UserId, Date = new DateOnly(2026, 6, 1) });
        db.WorkoutEntries.AddRange(
            new WorkoutEntry { Id = 1, SessionId = 1, ExerciseId = "ex1", ExerciseName = "A", Target = "chest", OrderIndex = 0 },
            new WorkoutEntry { Id = 2, SessionId = 1, ExerciseId = "ex2", ExerciseName = "B", Target = "back", OrderIndex = 1 });
        await db.SaveChangesAsync();
        var service = new WorkoutService(db);

        Assert.True(await service.ReorderEntriesAsync(UserId, 1, [2, 1], default));

        Assert.Equal(0, (await db.WorkoutEntries.FindAsync(2))!.OrderIndex);
        Assert.Equal(1, (await db.WorkoutEntries.FindAsync(1))!.OrderIndex);
    }

    [Fact]
    public async Task AddSetAutoIncrementsSetNumber()
    {
        await using var db = NewDb();
        db.WorkoutSessions.Add(new WorkoutSession { Id = 1, UserId = UserId, Date = new DateOnly(2026, 6, 1) });
        db.WorkoutEntries.Add(new WorkoutEntry { Id = 1, SessionId = 1, ExerciseId = "ex1", ExerciseName = "Bench", Target = "chest", OrderIndex = 0 });
        db.WorkoutSets.Add(new WorkoutSet { Id = 1, EntryId = 1, SetNumber = 1, Weight = 60, Reps = 10 });
        await db.SaveChangesAsync();
        var service = new WorkoutService(db);

        var set = await service.AddSetAsync(UserId, 1, 70, 8, default);

        Assert.NotNull(set);
        Assert.Equal(2, set!.SetNumber);
        Assert.Equal(70, set.Weight);
        Assert.Equal(8, set.Reps);
    }

    [Fact]
    public async Task AddSetReturnsNullForUnownedEntry()
    {
        await using var db = NewDb();
        db.WorkoutSessions.Add(new WorkoutSession { Id = 1, UserId = OtherUserId, Date = new DateOnly(2026, 6, 1) });
        db.WorkoutEntries.Add(new WorkoutEntry { Id = 1, SessionId = 1, ExerciseId = "ex1", ExerciseName = "Bench", Target = "chest", OrderIndex = 0 });
        await db.SaveChangesAsync();
        var service = new WorkoutService(db);

        Assert.Null(await service.AddSetAsync(UserId, 1, 70, 8, default));
    }

    [Fact]
    public async Task UpdateSetAppliesOnlyProvidedFields()
    {
        await using var db = NewDb();
        db.WorkoutSessions.Add(new WorkoutSession { Id = 1, UserId = UserId, Date = new DateOnly(2026, 6, 1) });
        db.WorkoutEntries.Add(new WorkoutEntry { Id = 1, SessionId = 1, ExerciseId = "ex1", ExerciseName = "Bench", Target = "chest", OrderIndex = 0 });
        db.WorkoutSets.Add(new WorkoutSet { Id = 1, EntryId = 1, SetNumber = 1, Weight = 60, Reps = 10 });
        await db.SaveChangesAsync();
        var service = new WorkoutService(db);

        Assert.True(await service.UpdateSetAsync(UserId, 1, weight: 65, reps: null, setNumber: null, completed: true, default));

        var set = await db.WorkoutSets.FindAsync(1);
        Assert.Equal(65, set!.Weight);
        Assert.Equal(10, set.Reps); // 변경 안 함
        Assert.True(set.Completed);
    }

    [Fact]
    public async Task DeleteSetRespectsOwnership()
    {
        await using var db = NewDb();
        db.WorkoutSessions.Add(new WorkoutSession { Id = 1, UserId = UserId, Date = new DateOnly(2026, 6, 1) });
        db.WorkoutEntries.Add(new WorkoutEntry { Id = 1, SessionId = 1, ExerciseId = "ex1", ExerciseName = "Bench", Target = "chest", OrderIndex = 0 });
        db.WorkoutSets.Add(new WorkoutSet { Id = 1, EntryId = 1, SetNumber = 1, Weight = 60, Reps = 10 });
        await db.SaveChangesAsync();
        var service = new WorkoutService(db);

        Assert.False(await service.DeleteSetAsync(OtherUserId, 1, default));
        Assert.True(await service.DeleteSetAsync(UserId, 1, default));
        Assert.Empty(await db.WorkoutSets.ToListAsync());
    }

    [Fact]
    public async Task CopyEntriesCopiesExercisesAndSetsVerbatimToTargetDate()
    {
        await using var db = NewDb();
        db.WorkoutSessions.Add(new WorkoutSession { Id = 1, UserId = UserId, Date = new DateOnly(2026, 6, 1) });
        db.WorkoutEntries.Add(new WorkoutEntry { Id = 1, SessionId = 1, ExerciseId = "ex1", ExerciseName = "Bench", Target = "chest", OrderIndex = 0 });
        db.WorkoutSets.Add(new WorkoutSet { Id = 1, EntryId = 1, SetNumber = 1, Weight = 60, Reps = 10 });
        await db.SaveChangesAsync();
        var service = new WorkoutService(db);

        var copied = await service.CopyEntriesAsync(UserId, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 2), default);

        Assert.Equal(1, copied);
        var target = await service.GetEntriesOfDateAsync(UserId, new DateOnly(2026, 6, 2), default);
        var entry = Assert.Single(target);
        Assert.Equal("ex1", entry.ExerciseId);
        var set = Assert.Single(entry.Sets);
        Assert.Equal((60.0, 10), (set.Weight, set.Reps));
    }

    [Fact]
    public async Task CopyEntriesReturnsZeroWhenSourceEmpty()
    {
        await using var db = NewDb();
        var service = new WorkoutService(db);

        Assert.Equal(0, await service.CopyEntriesAsync(UserId, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 2), default));
        Assert.Empty(await db.WorkoutSessions.ToListAsync());
    }

    [Fact]
    public async Task LastRecordExcludesGivenSessionAndReturnsNullWhenNoSets()
    {
        await using var db = NewDb();
        db.WorkoutSessions.AddRange(
            new WorkoutSession { Id = 1, UserId = UserId, Date = new DateOnly(2026, 5, 1) },
            new WorkoutSession { Id = 2, UserId = UserId, Date = new DateOnly(2026, 6, 1) });
        db.WorkoutEntries.AddRange(
            new WorkoutEntry { Id = 1, SessionId = 1, ExerciseId = "ex1", ExerciseName = "Bench", Target = "chest", OrderIndex = 0 },
            new WorkoutEntry { Id = 2, SessionId = 2, ExerciseId = "ex1", ExerciseName = "Bench", Target = "chest", OrderIndex = 0 });
        db.WorkoutSets.Add(new WorkoutSet { Id = 1, EntryId = 1, SetNumber = 1, Weight = 60, Reps = 10 });
        await db.SaveChangesAsync();
        var service = new WorkoutService(db);

        var record = await service.GetLastRecordAsync(UserId, "ex1", excludeSessionId: 2, default);
        Assert.NotNull(record);
        Assert.Equal(new DateOnly(2026, 5, 1), record!.Date);
        Assert.Single(record.Sets);

        // 세션2(세트 없음)를 제외하지 않으면, 더 최근이지만 세트가 없어 null.
        var noExclude = await service.GetLastRecordAsync(UserId, "ex1", excludeSessionId: null, default);
        Assert.Null(noExclude);
    }

    private static GymHubDbContext NewDb() =>
        new(new DbContextOptionsBuilder<GymHubDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
