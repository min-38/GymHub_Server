using GymHub.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace GymHub.Server.Tests;

public sealed class GymHubDbContextTests
{
    [Fact]
    public void ModelMapsGymHubTables()
    {
        using var context = CreateContext();

        Assert.Equal("exercises", context.Model.FindEntityType(typeof(Exercise))?.GetTableName());
        Assert.Equal("workout_sessions", context.Model.FindEntityType(typeof(WorkoutSession))?.GetTableName());
        Assert.Equal("workout_entries", context.Model.FindEntityType(typeof(WorkoutEntry))?.GetTableName());
        Assert.Equal("workout_sets", context.Model.FindEntityType(typeof(WorkoutSet))?.GetTableName());
        Assert.Equal("body_measurements", context.Model.FindEntityType(typeof(BodyMeasurement))?.GetTableName());
        Assert.Equal("inbody_records", context.Model.FindEntityType(typeof(InbodyRecord))?.GetTableName());
        Assert.Equal("app_meta", context.Model.FindEntityType(typeof(AppMeta))?.GetTableName());
        Assert.Equal("exercise_name_overrides", context.Model.FindEntityType(typeof(ExerciseNameOverride))?.GetTableName());
        Assert.Equal("routines", context.Model.FindEntityType(typeof(Routine))?.GetTableName());
        Assert.Equal("routine_exercises", context.Model.FindEntityType(typeof(RoutineExercise))?.GetTableName());
    }

    [Fact]
    public void ModelMapsCascadeWorkoutRelationships()
    {
        using var context = CreateContext();
        var entry = context.Model.FindEntityType(typeof(WorkoutEntry));
        var set = context.Model.FindEntityType(typeof(WorkoutSet));

        var entrySessionFk = Assert.Single(entry!.GetForeignKeys());
        var setEntryFk = Assert.Single(set!.GetForeignKeys());

        Assert.Equal(DeleteBehavior.Cascade, entrySessionFk.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Cascade, setEntryFk.DeleteBehavior);
        Assert.Equal("session_id", entrySessionFk.Properties.Single().GetColumnName());
        Assert.Equal("entry_id", setEntryFk.Properties.Single().GetColumnName());
    }

    [Fact]
    public void ModelMapsRoutineRelationship()
    {
        using var context = CreateContext();
        var routineExercise = context.Model.FindEntityType(typeof(RoutineExercise));
        var routineFk = Assert.Single(routineExercise!.GetForeignKeys());

        Assert.Equal(DeleteBehavior.Cascade, routineFk.DeleteBehavior);
        Assert.Equal("routine_id", routineFk.Properties.Single().GetColumnName());
    }

    private static GymHubDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GymHubDbContext>()
            .UseNpgsql("Host=localhost;Database=gymhub;Username=gymhub;Password=secret")
            .Options;

        return new GymHubDbContext(options);
    }
}
