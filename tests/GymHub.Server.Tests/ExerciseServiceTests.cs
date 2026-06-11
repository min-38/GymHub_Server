using GymHub.Server.Data;
using GymHub.Server.Exercises;
using Microsoft.EntityFrameworkCore;

namespace GymHub.Server.Tests;

public sealed class ExerciseServiceTests
{
    private static ExerciseQuery Query(
        string? search = null,
        string? bodyPart = null,
        string? target = null,
        string? equipment = null,
        int limit = 100,
        int offset = 0) =>
        new(search, bodyPart, target, equipment, limit, offset);

    [Fact]
    public async Task QueryReturnsAllSortedByKoreanName()
    {
        await using var db = NewSeededDb();
        var service = new ExerciseService(db);

        var page = await service.QueryAsync(Query(), default);

        Assert.Equal(3, page.Total);
        Assert.False(page.HasMore);
        // 바이셉컬 < 벤치프레스 < 스쿼트
        Assert.Equal(["ex3", "ex1", "ex2"], page.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task SearchIsCaseInsensitiveAcrossEnglishAndKorean()
    {
        await using var db = NewSeededDb();
        var service = new ExerciseService(db);

        var byEnglish = await service.QueryAsync(Query(search: "squat"), default);
        Assert.Equal(["ex2"], byEnglish.Items.Select(i => i.Id));

        var byKoreanEquipment = await service.QueryAsync(Query(search: "바벨"), default);
        Assert.Equal(["ex1", "ex2"], byKoreanEquipment.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task BodyPartFilterUsesKoreanColumn()
    {
        await using var db = NewSeededDb();
        var service = new ExerciseService(db);

        var page = await service.QueryAsync(Query(bodyPart: "가슴"), default);

        Assert.Equal(["ex1"], page.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task EquipmentAndTargetFilterUseEnglishColumns()
    {
        await using var db = NewSeededDb();
        var service = new ExerciseService(db);

        var byEquipment = await service.QueryAsync(Query(equipment: "barbell"), default);
        Assert.Equal(["ex1", "ex2"], byEquipment.Items.Select(i => i.Id));

        var byTarget = await service.QueryAsync(Query(target: "biceps"), default);
        Assert.Equal(["ex3"], byTarget.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task PaginationReportsTotalOffsetAndHasMore()
    {
        await using var db = NewSeededDb();
        var service = new ExerciseService(db);

        var first = await service.QueryAsync(Query(limit: 2, offset: 0), default);
        Assert.Equal(2, first.Items.Count);
        Assert.Equal(3, first.Total);
        Assert.True(first.HasMore);

        var second = await service.QueryAsync(Query(limit: 2, offset: 2), default);
        Assert.Single(second.Items);
        Assert.Equal(3, second.Total);
        Assert.False(second.HasMore);
    }

    [Fact]
    public async Task GetByIdReturnsExerciseOrNull()
    {
        await using var db = NewSeededDb();
        var service = new ExerciseService(db);

        var found = await service.GetByIdAsync("ex1", default);
        Assert.NotNull(found);
        Assert.Equal("벤치프레스", found!.NameKo);

        Assert.Null(await service.GetByIdAsync("missing", default));
    }

    [Fact]
    public async Task OverrideReplacesEffectiveKoreanNameInReads()
    {
        await using var db = NewSeededDb();
        var service = new ExerciseService(db);

        await service.SetNameOverrideAsync("ex1", "내가슴운동", default);

        var byId = await service.GetByIdAsync("ex1", default);
        Assert.Equal("내가슴운동", byId!.NameKo);

        // 오버라이드된 이름으로 검색되고, 기존 기본 한글명으로는 더 이상 매칭되지 않는다.
        var byOverride = await service.QueryAsync(Query(search: "내가슴"), default);
        Assert.Equal(["ex1"], byOverride.Items.Select(i => i.Id));

        var byOldName = await service.QueryAsync(Query(search: "벤치프레스"), default);
        Assert.Empty(byOldName.Items);
    }

    [Fact]
    public async Task SetNameOverrideCreatesUpdatesAndClears()
    {
        await using var db = NewSeededDb();
        var service = new ExerciseService(db);

        Assert.True(await service.SetNameOverrideAsync("ex1", "이름1", default));
        Assert.Equal("이름1", (await service.GetNameOverridesAsync(default))["ex1"]);

        Assert.True(await service.SetNameOverrideAsync("ex1", "이름2", default));
        Assert.Equal("이름2", (await service.GetNameOverridesAsync(default))["ex1"]);

        // 빈 값으로 설정하면 오버라이드가 제거되고 기본 한글명이 복원된다.
        Assert.True(await service.SetNameOverrideAsync("ex1", "  ", default));
        Assert.Empty(await service.GetNameOverridesAsync(default));
        Assert.Equal("벤치프레스", (await service.GetByIdAsync("ex1", default))!.NameKo);
    }

    [Fact]
    public async Task SetNameOverrideReturnsFalseForUnknownExercise()
    {
        await using var db = NewSeededDb();
        var service = new ExerciseService(db);

        Assert.False(await service.SetNameOverrideAsync("missing", "이름", default));
    }

    private static GymHubDbContext NewSeededDb()
    {
        var db = new GymHubDbContext(new DbContextOptionsBuilder<GymHubDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        db.Exercises.AddRange(
            NewExercise("ex1", "Bench Press", "벤치프레스", "chest", "가슴", "pectorals", "대흉근", "barbell", "바벨"),
            NewExercise("ex2", "Squat", "스쿼트", "upper legs", "하체", "quads", "대퇴사두", "barbell", "바벨"),
            NewExercise("ex3", "Bicep Curl", "바이셉컬", "upper arms", "이두", "biceps", "이두근", "dumbbell", "덤벨"));
        db.SaveChanges();
        return db;
    }

    private static Exercise NewExercise(
        string id, string name, string nameKo,
        string bodyPart, string bodyPartKo,
        string target, string targetKo,
        string equipment, string equipmentKo) =>
        new()
        {
            Id = id,
            Name = name,
            NameKo = nameKo,
            BodyPart = bodyPart,
            BodyPartKo = bodyPartKo,
            Target = target,
            TargetKo = targetKo,
            Equipment = equipment,
            EquipmentKo = equipmentKo,
            GifUrl = $"https://gif/{id}",
        };
}
