using Microsoft.EntityFrameworkCore;

namespace GymHub.Server.Data;

public sealed class GymHubDbContext(DbContextOptions<GymHubDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Exercise> Exercises => Set<Exercise>();

    public DbSet<WorkoutSession> WorkoutSessions => Set<WorkoutSession>();

    public DbSet<WorkoutEntry> WorkoutEntries => Set<WorkoutEntry>();

    public DbSet<WorkoutSet> WorkoutSets => Set<WorkoutSet>();

    public DbSet<BodyMeasurement> BodyMeasurements => Set<BodyMeasurement>();

    public DbSet<InbodyRecord> InbodyRecords => Set<InbodyRecord>();

    public DbSet<Profile> Profiles => Set<Profile>();

    public DbSet<AppMeta> AppMeta => Set<AppMeta>();

    public DbSet<ExerciseNameOverride> ExerciseNameOverrides => Set<ExerciseNameOverride>();

    public DbSet<Routine> Routines => Set<Routine>();

    public DbSet<RoutineExercise> RoutineExercises => Set<RoutineExercise>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(e => e.Email).HasColumnName("email").IsRequired();
            entity.Property(e => e.GoogleSub).HasColumnName("google_sub");
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
            entity.Property(e => e.DisplayName).HasColumnName("display_name").HasDefaultValue("").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasIndex(e => e.Email).IsUnique().HasDatabaseName("idx_users_email");
            entity.HasIndex(e => e.GoogleSub).IsUnique().HasDatabaseName("idx_users_google_sub");
        });

        modelBuilder.Entity<Exercise>(entity =>
        {
            entity.ToTable("exercises");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.NameKo).HasColumnName("name_ko").HasDefaultValue("").IsRequired();
            entity.Property(e => e.BodyPart).HasColumnName("body_part").IsRequired();
            entity.Property(e => e.BodyPartKo).HasColumnName("body_part_ko").HasDefaultValue("").IsRequired();
            entity.Property(e => e.Target).HasColumnName("target").IsRequired();
            entity.Property(e => e.TargetKo).HasColumnName("target_ko").HasDefaultValue("").IsRequired();
            entity.Property(e => e.Equipment).HasColumnName("equipment").IsRequired();
            entity.Property(e => e.EquipmentKo).HasColumnName("equipment_ko").HasDefaultValue("").IsRequired();
            entity.Property(e => e.GifUrl).HasColumnName("gif_url").IsRequired();
            entity.Property(e => e.SecondaryMuscles).HasColumnName("secondary_muscles");
            entity.Property(e => e.Instructions).HasColumnName("instructions");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.GifLocalPath).HasColumnName("gif_local_path");
            entity.Property(e => e.GifCachedAt).HasColumnName("gif_cached_at");
            entity.HasIndex(e => e.Target).HasDatabaseName("idx_exercises_target");
            entity.HasIndex(e => e.Equipment).HasDatabaseName("idx_exercises_equipment");
            entity.HasIndex(e => e.BodyPart).HasDatabaseName("idx_exercises_body_part");
            entity.HasIndex(e => e.NameKo).HasDatabaseName("idx_exercises_name_ko");
            entity.HasIndex(e => e.BodyPartKo).HasDatabaseName("idx_exercises_body_part_ko");
        });

        modelBuilder.Entity<WorkoutSession>(entity =>
        {
            entity.ToTable("workout_sessions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Date).HasColumnName("date").HasColumnType("date");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.DurationSec).HasColumnName("duration_sec").HasDefaultValue(0);
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.UserId, e.Date }).IsUnique().HasDatabaseName("idx_sessions_user_date");
        });

        modelBuilder.Entity<WorkoutEntry>(entity =>
        {
            entity.ToTable("workout_entries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.ExerciseId).HasColumnName("exercise_id").IsRequired();
            entity.Property(e => e.ExerciseName).HasColumnName("exercise_name").IsRequired();
            entity.Property(e => e.Target).HasColumnName("target").IsRequired();
            entity.Property(e => e.OrderIndex).HasColumnName("order_index");
            entity.Property(e => e.RestSec).HasColumnName("rest_sec");
            entity.Property(e => e.SupersetGroup).HasColumnName("superset_group");
            entity.HasOne(e => e.Session)
                .WithMany(e => e.Entries)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.SessionId).HasDatabaseName("idx_entries_session");
            entity.HasIndex(e => e.ExerciseId).HasDatabaseName("idx_entries_exercise");
            entity.HasIndex(e => e.Target).HasDatabaseName("idx_entries_target");
        });

        modelBuilder.Entity<WorkoutSet>(entity =>
        {
            entity.ToTable("workout_sets");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(e => e.EntryId).HasColumnName("entry_id");
            entity.Property(e => e.SetNumber).HasColumnName("set_number");
            entity.Property(e => e.Weight).HasColumnName("weight");
            entity.Property(e => e.Reps).HasColumnName("reps");
            entity.Property(e => e.Completed).HasColumnName("completed").HasDefaultValue(false);
            entity.HasOne(e => e.Entry)
                .WithMany(e => e.Sets)
                .HasForeignKey(e => e.EntryId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.EntryId).HasDatabaseName("idx_sets_entry");
        });

        modelBuilder.Entity<BodyMeasurement>(entity =>
        {
            entity.ToTable("body_measurements");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Date).HasColumnName("date").HasColumnType("date");
            entity.Property(e => e.Weight).HasColumnName("weight");
            entity.Property(e => e.Height).HasColumnName("height");
            entity.Property(e => e.Chest).HasColumnName("chest");
            entity.Property(e => e.Waist).HasColumnName("waist");
            entity.Property(e => e.Hip).HasColumnName("hip");
            entity.Property(e => e.Arm).HasColumnName("arm");
            entity.Property(e => e.Thigh).HasColumnName("thigh");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.UserId, e.Date }).HasDatabaseName("idx_body_user_date");
        });

        modelBuilder.Entity<InbodyRecord>(entity =>
        {
            entity.ToTable("inbody_records");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Date).HasColumnName("date").HasColumnType("date");
            entity.Property(e => e.Weight).HasColumnName("weight");
            entity.Property(e => e.BodyFatPercentage).HasColumnName("body_fat_percentage");
            entity.Property(e => e.MuscleMass).HasColumnName("muscle_mass");
            entity.Property(e => e.Bmi).HasColumnName("bmi");
            entity.Property(e => e.BodyWater).HasColumnName("body_water");
            entity.Property(e => e.Bmr).HasColumnName("bmr");
            entity.Property(e => e.VisceralFat).HasColumnName("visceral_fat");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.UserId, e.Date }).HasDatabaseName("idx_inbody_user_date");
        });

        modelBuilder.Entity<Profile>(entity =>
        {
            entity.ToTable("profiles");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Gender).HasColumnName("gender");
            entity.Property(e => e.BirthDate).HasColumnName("birth_date").HasColumnType("date");
            entity.Property(e => e.Height).HasColumnName("height");
            entity.Property(e => e.Weight).HasColumnName("weight");
            entity.HasOne<User>().WithOne().HasForeignKey<Profile>(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppMeta>(entity =>
        {
            entity.ToTable("app_meta");
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasColumnName("key");
            entity.Property(e => e.Value).HasColumnName("value").IsRequired();
        });

        modelBuilder.Entity<ExerciseNameOverride>(entity =>
        {
            entity.ToTable("exercise_name_overrides");
            entity.HasKey(e => e.ExerciseId);
            entity.Property(e => e.ExerciseId).HasColumnName("exercise_id");
            entity.Property(e => e.NameKo).HasColumnName("name_ko").IsRequired();
        });

        modelBuilder.Entity<Routine>(entity =>
        {
            entity.ToTable("routines");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.OrderIndex).HasColumnName("order_index").HasDefaultValue(0);
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.UserId).HasDatabaseName("idx_routines_user");
        });

        modelBuilder.Entity<RoutineExercise>(entity =>
        {
            entity.ToTable("routine_exercises");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(e => e.RoutineId).HasColumnName("routine_id");
            entity.Property(e => e.ExerciseId).HasColumnName("exercise_id").IsRequired();
            entity.Property(e => e.ExerciseName).HasColumnName("exercise_name").IsRequired();
            entity.Property(e => e.Target).HasColumnName("target").IsRequired();
            entity.Property(e => e.OrderIndex).HasColumnName("order_index");
            entity.Property(e => e.DefaultSets).HasColumnName("default_sets").HasDefaultValue(3);
            entity.Property(e => e.DefaultReps).HasColumnName("default_reps").HasDefaultValue(10);
            entity.Property(e => e.DefaultWeight).HasColumnName("default_weight").HasDefaultValue(0);
            entity.HasOne(e => e.Routine)
                .WithMany(e => e.Exercises)
                .HasForeignKey(e => e.RoutineId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.RoutineId).HasDatabaseName("idx_routine_exercises_routine");
        });
    }
}

public sealed class Exercise
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public string NameKo { get; set; } = "";
    public required string BodyPart { get; set; }
    public string BodyPartKo { get; set; } = "";
    public required string Target { get; set; }
    public string TargetKo { get; set; } = "";
    public required string Equipment { get; set; }
    public string EquipmentKo { get; set; } = "";
    public required string GifUrl { get; set; }
    public string? SecondaryMuscles { get; set; }
    public string? Instructions { get; set; }
    public string? Description { get; set; }
    public string? GifLocalPath { get; set; }
    public DateTimeOffset? GifCachedAt { get; set; }
}

public sealed class User
{
    public int Id { get; init; }
    public required string Email { get; set; }
    public string? GoogleSub { get; set; }
    public string? PasswordHash { get; set; }
    public string DisplayName { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class WorkoutSession
{
    public int Id { get; init; }
    public int UserId { get; set; }
    public DateOnly Date { get; set; }
    public string? Note { get; set; }
    public int DurationSec { get; set; }
    public List<WorkoutEntry> Entries { get; } = [];
}

public sealed class WorkoutEntry
{
    public int Id { get; init; }
    public int SessionId { get; set; }
    public required string ExerciseId { get; set; }
    public required string ExerciseName { get; set; }
    public required string Target { get; set; }
    public int OrderIndex { get; set; }

    /// <summary>Per-exercise rest seconds. Null = use the app's global default.</summary>
    public int? RestSec { get; set; }

    /// <summary>Superset group id. Entries sharing a value form one superset. Null = standalone.</summary>
    public int? SupersetGroup { get; set; }
    public WorkoutSession? Session { get; set; }
    public List<WorkoutSet> Sets { get; } = [];
}

public sealed class WorkoutSet
{
    public int Id { get; init; }
    public int EntryId { get; set; }
    public int SetNumber { get; set; }
    public double Weight { get; set; }
    public int Reps { get; set; }
    public bool Completed { get; set; }
    public WorkoutEntry? Entry { get; set; }
}

public sealed class BodyMeasurement
{
    public int Id { get; init; }
    public int UserId { get; set; }
    public DateOnly Date { get; set; }
    public double Weight { get; set; }
    public double? Height { get; set; }
    public double? Chest { get; set; }
    public double? Waist { get; set; }
    public double? Hip { get; set; }
    public double? Arm { get; set; }
    public double? Thigh { get; set; }
    public string? Note { get; set; }
}

public sealed class InbodyRecord
{
    public int Id { get; init; }
    public int UserId { get; set; }
    public DateOnly Date { get; set; }
    public double Weight { get; set; }
    public double? BodyFatPercentage { get; set; }
    public double? MuscleMass { get; set; }
    public double? Bmi { get; set; }
    public double? BodyWater { get; set; }
    public double? Bmr { get; set; }
    public int? VisceralFat { get; set; }
    public string? Note { get; set; }
}

public sealed class Profile
{
    public int UserId { get; init; }
    public string? Name { get; set; }

    /// <summary>'male' | 'female' | null</summary>
    public string? Gender { get; set; }
    public DateOnly? BirthDate { get; set; }

    /// <summary>cm</summary>
    public double? Height { get; set; }

    /// <summary>kg</summary>
    public double? Weight { get; set; }
}

public sealed class AppMeta
{
    public required string Key { get; init; }
    public required string Value { get; set; }
}

public sealed class ExerciseNameOverride
{
    public required string ExerciseId { get; init; }
    public required string NameKo { get; set; }
}

public sealed class Routine
{
    public int Id { get; init; }
    public int UserId { get; set; }
    public required string Name { get; set; }
    public string? Note { get; set; }
    public int OrderIndex { get; set; }
    public List<RoutineExercise> Exercises { get; } = [];
}

public sealed class RoutineExercise
{
    public int Id { get; init; }
    public int RoutineId { get; set; }
    public required string ExerciseId { get; set; }
    public required string ExerciseName { get; set; }
    public required string Target { get; set; }
    public int OrderIndex { get; set; }
    public int DefaultSets { get; set; } = 3;
    public int DefaultReps { get; set; } = 10;
    public double DefaultWeight { get; set; }
    public Routine? Routine { get; set; }
}
