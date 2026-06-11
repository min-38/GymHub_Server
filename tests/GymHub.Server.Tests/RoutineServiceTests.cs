using GymHub.Server.Data;
using GymHub.Server.Routines;
using Microsoft.EntityFrameworkCore;

namespace GymHub.Server.Tests;

public sealed class RoutineServiceTests
{
    private const int UserId = 1;
    private const int OtherUserId = 2;

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyOwnRoutines_WithExercises_OrderedByOrderIndex()
    {
        using var db = CreateContext();
        var r1 = new Routine { UserId = UserId, Name = "Push", OrderIndex = 1 };
        var r2 = new Routine { UserId = UserId, Name = "Pull", OrderIndex = 0 };
        var other = new Routine { UserId = OtherUserId, Name = "Legs", OrderIndex = 0 };
        db.Routines.AddRange(r1, r2, other);
        await db.SaveChangesAsync();
        db.RoutineExercises.AddRange(
            new RoutineExercise { RoutineId = r1.Id, ExerciseId = "e2", ExerciseName = "Dip", Target = "triceps", OrderIndex = 1 },
            new RoutineExercise { RoutineId = r1.Id, ExerciseId = "e1", ExerciseName = "Bench", Target = "chest", OrderIndex = 0 });
        await db.SaveChangesAsync();
        var service = new RoutineService(db);

        var result = await service.GetAllAsync(UserId, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("Pull", result[0].Name);
        Assert.Equal("Push", result[1].Name);
        Assert.Equal(["Bench", "Dip"], result[1].Exercises.Select(e => e.ExerciseName));
    }

    [Fact]
    public async Task GetByIdAsync_OwnedByAnotherUser_ReturnsNull()
    {
        using var db = CreateContext();
        var routine = new Routine { UserId = OtherUserId, Name = "Legs" };
        db.Routines.Add(routine);
        await db.SaveChangesAsync();
        var service = new RoutineService(db);

        var result = await service.GetByIdAsync(UserId, routine.Id, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_AssignsIncrementingOrderIndexPerUser()
    {
        using var db = CreateContext();
        var service = new RoutineService(db);

        var first = await service.CreateAsync(UserId, "A", null, CancellationToken.None);
        var second = await service.CreateAsync(UserId, "B", "note", CancellationToken.None);

        Assert.Equal(0, first.OrderIndex);
        Assert.Equal(1, second.OrderIndex);
        Assert.Equal("note", second.Note);
    }

    [Fact]
    public async Task CreateUntitledAsync_FirstIsUntitled_SubsequentAreNumbered()
    {
        using var db = CreateContext();
        var service = new RoutineService(db);

        var first = await service.CreateUntitledAsync(UserId, CancellationToken.None);
        var second = await service.CreateUntitledAsync(UserId, CancellationToken.None);
        var third = await service.CreateUntitledAsync(UserId, CancellationToken.None);

        Assert.Equal("untitled", first.Name);
        Assert.Equal("untitled-1", second.Name);
        Assert.Equal("untitled-2", third.Name);
    }

    [Fact]
    public async Task CreateUntitledAsync_NamesAreScopedPerUser()
    {
        using var db = CreateContext();
        db.Routines.Add(new Routine { UserId = OtherUserId, Name = "untitled" });
        await db.SaveChangesAsync();
        var service = new RoutineService(db);

        var result = await service.CreateUntitledAsync(UserId, CancellationToken.None);

        Assert.Equal("untitled", result.Name);
    }

    [Fact]
    public async Task RenameAsync_OwnRoutine_UpdatesNameAndNote()
    {
        using var db = CreateContext();
        var routine = new Routine { UserId = UserId, Name = "Old", Note = "old note" };
        db.Routines.Add(routine);
        await db.SaveChangesAsync();
        var service = new RoutineService(db);

        var ok = await service.RenameAsync(UserId, routine.Id, "New", null, CancellationToken.None);

        Assert.True(ok);
        var stored = await db.Routines.SingleAsync();
        Assert.Equal("New", stored.Name);
        Assert.Null(stored.Note);
    }

    [Fact]
    public async Task RenameAsync_OwnedByAnotherUser_ReturnsFalse()
    {
        using var db = CreateContext();
        var routine = new Routine { UserId = OtherUserId, Name = "Old" };
        db.Routines.Add(routine);
        await db.SaveChangesAsync();
        var service = new RoutineService(db);

        var ok = await service.RenameAsync(UserId, routine.Id, "New", null, CancellationToken.None);

        Assert.False(ok);
    }

    [Fact]
    public async Task DeleteAsync_OwnRoutine_RemovesRoutineAndExercises()
    {
        using var db = CreateContext();
        var routine = new Routine { UserId = UserId, Name = "R" };
        db.Routines.Add(routine);
        await db.SaveChangesAsync();
        db.RoutineExercises.Add(new RoutineExercise { RoutineId = routine.Id, ExerciseId = "e1", ExerciseName = "Bench", Target = "chest", OrderIndex = 0 });
        await db.SaveChangesAsync();
        var service = new RoutineService(db);

        var ok = await service.DeleteAsync(UserId, routine.Id, CancellationToken.None);

        Assert.True(ok);
        Assert.Empty(db.Routines);
        Assert.Empty(db.RoutineExercises);
    }

    [Fact]
    public async Task DeleteAsync_OwnedByAnotherUser_ReturnsFalse()
    {
        using var db = CreateContext();
        var routine = new Routine { UserId = OtherUserId, Name = "R" };
        db.Routines.Add(routine);
        await db.SaveChangesAsync();
        var service = new RoutineService(db);

        var ok = await service.DeleteAsync(UserId, routine.Id, CancellationToken.None);

        Assert.False(ok);
        Assert.Single(db.Routines);
    }

    [Fact]
    public async Task AddExerciseAsync_OwnRoutine_AppendsWithDefaultsAndOrder()
    {
        using var db = CreateContext();
        var routine = new Routine { UserId = UserId, Name = "R" };
        db.Routines.Add(routine);
        await db.SaveChangesAsync();
        var service = new RoutineService(db);

        var first = await service.AddExerciseAsync(UserId, routine.Id, "e1", "Bench", "chest", CancellationToken.None);
        var second = await service.AddExerciseAsync(UserId, routine.Id, "e2", "Dip", "triceps", CancellationToken.None);

        Assert.NotNull(first);
        Assert.Equal(0, first!.OrderIndex);
        Assert.Equal(3, first.DefaultSets);
        Assert.Equal(10, first.DefaultReps);
        Assert.Equal(0, first.DefaultWeight);
        Assert.Equal(1, second!.OrderIndex);
    }

    [Fact]
    public async Task AddExerciseAsync_OwnedByAnotherUser_ReturnsNull()
    {
        using var db = CreateContext();
        var routine = new Routine { UserId = OtherUserId, Name = "R" };
        db.Routines.Add(routine);
        await db.SaveChangesAsync();
        var service = new RoutineService(db);

        var result = await service.AddExerciseAsync(UserId, routine.Id, "e1", "Bench", "chest", CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(db.RoutineExercises);
    }

    [Fact]
    public async Task CreateFromSessionAsync_DerivesDefaultsFromRecordedSets()
    {
        using var db = CreateContext();
        var session = new WorkoutSession { UserId = UserId, Date = new DateOnly(2026, 1, 1) };
        db.WorkoutSessions.Add(session);
        await db.SaveChangesAsync();
        var entry = new WorkoutEntry { SessionId = session.Id, ExerciseId = "e1", ExerciseName = "Bench", Target = "chest", OrderIndex = 0 };
        db.WorkoutEntries.Add(entry);
        await db.SaveChangesAsync();
        db.WorkoutSets.AddRange(
            new WorkoutSet { EntryId = entry.Id, SetNumber = 1, Weight = 0, Reps = 0 },
            new WorkoutSet { EntryId = entry.Id, SetNumber = 2, Weight = 60, Reps = 8 },
            new WorkoutSet { EntryId = entry.Id, SetNumber = 3, Weight = 65, Reps = 6 });
        await db.SaveChangesAsync();
        var service = new RoutineService(db);

        var routine = await service.CreateFromSessionAsync(UserId, "From workout", session.Id, CancellationToken.None);

        Assert.NotNull(routine);
        var ex = Assert.Single(routine!.Exercises);
        Assert.Equal(3, ex.DefaultSets);   // 3 sets recorded
        Assert.Equal(8, ex.DefaultReps);   // first set with a value
        Assert.Equal(60, ex.DefaultWeight);
    }

    [Fact]
    public async Task CreateFromSessionAsync_EmptyEntrySets_DefaultsToOneSetZeroValues()
    {
        using var db = CreateContext();
        var session = new WorkoutSession { UserId = UserId, Date = new DateOnly(2026, 1, 1) };
        db.WorkoutSessions.Add(session);
        await db.SaveChangesAsync();
        db.WorkoutEntries.Add(new WorkoutEntry { SessionId = session.Id, ExerciseId = "e1", ExerciseName = "Bench", Target = "chest", OrderIndex = 0 });
        await db.SaveChangesAsync();
        var service = new RoutineService(db);

        var routine = await service.CreateFromSessionAsync(UserId, "R", session.Id, CancellationToken.None);

        var ex = Assert.Single(routine!.Exercises);
        Assert.Equal(1, ex.DefaultSets);
        Assert.Equal(0, ex.DefaultReps);
        Assert.Equal(0, ex.DefaultWeight);
    }

    [Fact]
    public async Task CreateFromSessionAsync_SessionOwnedByAnotherUser_ReturnsNull()
    {
        using var db = CreateContext();
        var session = new WorkoutSession { UserId = OtherUserId, Date = new DateOnly(2026, 1, 1) };
        db.WorkoutSessions.Add(session);
        await db.SaveChangesAsync();
        var service = new RoutineService(db);

        var routine = await service.CreateFromSessionAsync(UserId, "R", session.Id, CancellationToken.None);

        Assert.Null(routine);
        Assert.Empty(db.Routines);
    }

    [Fact]
    public async Task UpdateExerciseDefaultsAsync_OwnExercise_UpdatesValues()
    {
        using var db = CreateContext();
        var routine = new Routine { UserId = UserId, Name = "R" };
        db.Routines.Add(routine);
        await db.SaveChangesAsync();
        var ex = new RoutineExercise { RoutineId = routine.Id, ExerciseId = "e1", ExerciseName = "Bench", Target = "chest", OrderIndex = 0 };
        db.RoutineExercises.Add(ex);
        await db.SaveChangesAsync();
        var service = new RoutineService(db);

        var ok = await service.UpdateExerciseDefaultsAsync(UserId, ex.Id, sets: 5, reps: 5, weight: 80, CancellationToken.None);

        Assert.True(ok);
        var stored = await db.RoutineExercises.SingleAsync();
        Assert.Equal(5, stored.DefaultSets);
        Assert.Equal(5, stored.DefaultReps);
        Assert.Equal(80, stored.DefaultWeight);
    }

    [Fact]
    public async Task UpdateExerciseDefaultsAsync_ExerciseInAnotherUsersRoutine_ReturnsFalse()
    {
        using var db = CreateContext();
        var routine = new Routine { UserId = OtherUserId, Name = "R" };
        db.Routines.Add(routine);
        await db.SaveChangesAsync();
        var ex = new RoutineExercise { RoutineId = routine.Id, ExerciseId = "e1", ExerciseName = "Bench", Target = "chest", OrderIndex = 0 };
        db.RoutineExercises.Add(ex);
        await db.SaveChangesAsync();
        var service = new RoutineService(db);

        var ok = await service.UpdateExerciseDefaultsAsync(UserId, ex.Id, 5, 5, 80, CancellationToken.None);

        Assert.False(ok);
    }

    [Fact]
    public async Task RemoveExerciseAsync_OwnExercise_RemovesIt()
    {
        using var db = CreateContext();
        var routine = new Routine { UserId = UserId, Name = "R" };
        db.Routines.Add(routine);
        await db.SaveChangesAsync();
        var ex = new RoutineExercise { RoutineId = routine.Id, ExerciseId = "e1", ExerciseName = "Bench", Target = "chest", OrderIndex = 0 };
        db.RoutineExercises.Add(ex);
        await db.SaveChangesAsync();
        var service = new RoutineService(db);

        var ok = await service.RemoveExerciseAsync(UserId, ex.Id, CancellationToken.None);

        Assert.True(ok);
        Assert.Empty(db.RoutineExercises);
    }

    [Fact]
    public async Task RemoveExerciseAsync_InAnotherUsersRoutine_ReturnsFalse()
    {
        using var db = CreateContext();
        var routine = new Routine { UserId = OtherUserId, Name = "R" };
        db.Routines.Add(routine);
        await db.SaveChangesAsync();
        var ex = new RoutineExercise { RoutineId = routine.Id, ExerciseId = "e1", ExerciseName = "Bench", Target = "chest", OrderIndex = 0 };
        db.RoutineExercises.Add(ex);
        await db.SaveChangesAsync();
        var service = new RoutineService(db);

        var ok = await service.RemoveExerciseAsync(UserId, ex.Id, CancellationToken.None);

        Assert.False(ok);
        Assert.Single(db.RoutineExercises);
    }

    [Fact]
    public async Task ReorderExercisesAsync_RenumbersToGivenOrder()
    {
        using var db = CreateContext();
        var routine = new Routine { UserId = UserId, Name = "R" };
        db.Routines.Add(routine);
        await db.SaveChangesAsync();
        var a = new RoutineExercise { RoutineId = routine.Id, ExerciseId = "e1", ExerciseName = "A", Target = "chest", OrderIndex = 0 };
        var b = new RoutineExercise { RoutineId = routine.Id, ExerciseId = "e2", ExerciseName = "B", Target = "back", OrderIndex = 1 };
        var c = new RoutineExercise { RoutineId = routine.Id, ExerciseId = "e3", ExerciseName = "C", Target = "legs", OrderIndex = 2 };
        db.RoutineExercises.AddRange(a, b, c);
        await db.SaveChangesAsync();
        var service = new RoutineService(db);

        var ok = await service.ReorderExercisesAsync(UserId, routine.Id, [c.Id, a.Id, b.Id], CancellationToken.None);

        Assert.True(ok);
        var ordered = await db.RoutineExercises.OrderBy(e => e.OrderIndex).Select(e => e.ExerciseName).ToListAsync();
        Assert.Equal(["C", "A", "B"], ordered);
    }

    [Fact]
    public async Task ReorderExercisesAsync_OwnedByAnotherUser_ReturnsFalse()
    {
        using var db = CreateContext();
        var routine = new Routine { UserId = OtherUserId, Name = "R" };
        db.Routines.Add(routine);
        await db.SaveChangesAsync();
        var service = new RoutineService(db);

        var ok = await service.ReorderExercisesAsync(UserId, routine.Id, [1, 2], CancellationToken.None);

        Assert.False(ok);
    }

    private static GymHubDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GymHubDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new GymHubDbContext(options);
    }
}
