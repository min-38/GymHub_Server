using GymHub.Server.Body;
using GymHub.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace GymHub.Server.Tests;

public sealed class BodyServiceTests
{
    private const int UserId = 1;
    private const int OtherUserId = 2;

    [Fact]
    public async Task GetMeasurementsAsync_ReturnsOnlyOwnRecords_OrderedByDateDescending()
    {
        using var db = CreateContext();
        db.BodyMeasurements.AddRange(
            new BodyMeasurement { UserId = UserId, Date = new DateOnly(2026, 1, 1), Weight = 70 },
            new BodyMeasurement { UserId = UserId, Date = new DateOnly(2026, 2, 1), Weight = 71 },
            new BodyMeasurement { UserId = OtherUserId, Date = new DateOnly(2026, 3, 1), Weight = 80 });
        await db.SaveChangesAsync();
        var service = new BodyService(db);

        var result = await service.GetMeasurementsAsync(UserId, chronological: false, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(new DateOnly(2026, 2, 1), result[0].Date);
        Assert.Equal(new DateOnly(2026, 1, 1), result[1].Date);
    }

    [Fact]
    public async Task GetMeasurementsAsync_Chronological_OrdersAscending()
    {
        using var db = CreateContext();
        db.BodyMeasurements.AddRange(
            new BodyMeasurement { UserId = UserId, Date = new DateOnly(2026, 2, 1), Weight = 71 },
            new BodyMeasurement { UserId = UserId, Date = new DateOnly(2026, 1, 1), Weight = 70 });
        await db.SaveChangesAsync();
        var service = new BodyService(db);

        var result = await service.GetMeasurementsAsync(UserId, chronological: true, CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 1, 1), result[0].Date);
        Assert.Equal(new DateOnly(2026, 2, 1), result[1].Date);
    }

    [Fact]
    public async Task UpsertMeasurementAsync_WithoutId_CreatesNewRecord()
    {
        using var db = CreateContext();
        var service = new BodyService(db);

        var result = await service.UpsertMeasurementAsync(UserId, new UpsertBodyMeasurementRequest(
            null, new DateOnly(2026, 1, 1), 70, 175, 90, 80, 95, 30, 55, "note"), CancellationToken.None);

        Assert.NotNull(result);
        var stored = await db.BodyMeasurements.SingleAsync();
        Assert.Equal(UserId, stored.UserId);
        Assert.Equal(70, stored.Weight);
        Assert.Equal("note", stored.Note);
    }

    [Fact]
    public async Task UpsertMeasurementAsync_WithId_UpdatesOwnRecord()
    {
        using var db = CreateContext();
        var existing = new BodyMeasurement { UserId = UserId, Date = new DateOnly(2026, 1, 1), Weight = 70 };
        db.BodyMeasurements.Add(existing);
        await db.SaveChangesAsync();
        var service = new BodyService(db);

        var result = await service.UpsertMeasurementAsync(UserId, new UpsertBodyMeasurementRequest(
            existing.Id, new DateOnly(2026, 1, 2), 72, null, null, null, null, null, null, null), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(72, result!.Weight);
        Assert.Equal(new DateOnly(2026, 1, 2), result.Date);
    }

    [Fact]
    public async Task UpsertMeasurementAsync_WithIdOwnedByAnotherUser_ReturnsNull()
    {
        using var db = CreateContext();
        var existing = new BodyMeasurement { UserId = OtherUserId, Date = new DateOnly(2026, 1, 1), Weight = 70 };
        db.BodyMeasurements.Add(existing);
        await db.SaveChangesAsync();
        var service = new BodyService(db);

        var result = await service.UpsertMeasurementAsync(UserId, new UpsertBodyMeasurementRequest(
            existing.Id, new DateOnly(2026, 1, 2), 72, null, null, null, null, null, null, null), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteMeasurementAsync_RemovesOwnRecord_ReturnsTrue()
    {
        using var db = CreateContext();
        var existing = new BodyMeasurement { UserId = UserId, Date = new DateOnly(2026, 1, 1), Weight = 70 };
        db.BodyMeasurements.Add(existing);
        await db.SaveChangesAsync();
        var service = new BodyService(db);

        var deleted = await service.DeleteMeasurementAsync(UserId, existing.Id, CancellationToken.None);

        Assert.True(deleted);
        Assert.Empty(db.BodyMeasurements);
    }

    [Fact]
    public async Task DeleteMeasurementAsync_OwnedByAnotherUser_ReturnsFalse()
    {
        using var db = CreateContext();
        var existing = new BodyMeasurement { UserId = OtherUserId, Date = new DateOnly(2026, 1, 1), Weight = 70 };
        db.BodyMeasurements.Add(existing);
        await db.SaveChangesAsync();
        var service = new BodyService(db);

        var deleted = await service.DeleteMeasurementAsync(UserId, existing.Id, CancellationToken.None);

        Assert.False(deleted);
        Assert.Single(db.BodyMeasurements);
    }

    [Fact]
    public async Task GetInbodyRecordsAsync_ReturnsOnlyOwnRecords_OrderedByDateDescending()
    {
        using var db = CreateContext();
        db.InbodyRecords.AddRange(
            new InbodyRecord { UserId = UserId, Date = new DateOnly(2026, 1, 1), Weight = 70 },
            new InbodyRecord { UserId = UserId, Date = new DateOnly(2026, 2, 1), Weight = 71 },
            new InbodyRecord { UserId = OtherUserId, Date = new DateOnly(2026, 3, 1), Weight = 80 });
        await db.SaveChangesAsync();
        var service = new BodyService(db);

        var result = await service.GetInbodyRecordsAsync(UserId, chronological: false, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(new DateOnly(2026, 2, 1), result[0].Date);
    }

    [Fact]
    public async Task UpsertInbodyRecordAsync_WithoutId_CreatesNewRecord()
    {
        using var db = CreateContext();
        var service = new BodyService(db);

        var result = await service.UpsertInbodyRecordAsync(UserId, new UpsertInbodyRecordRequest(
            null, new DateOnly(2026, 1, 1), 70, 18.5, 32.1, 23.0, 55.0, 1600, 8, "note"), CancellationToken.None);

        Assert.NotNull(result);
        var stored = await db.InbodyRecords.SingleAsync();
        Assert.Equal(UserId, stored.UserId);
        Assert.Equal(18.5, stored.BodyFatPercentage);
        Assert.Equal(8, stored.VisceralFat);
    }

    [Fact]
    public async Task UpsertInbodyRecordAsync_WithIdOwnedByAnotherUser_ReturnsNull()
    {
        using var db = CreateContext();
        var existing = new InbodyRecord { UserId = OtherUserId, Date = new DateOnly(2026, 1, 1), Weight = 70 };
        db.InbodyRecords.Add(existing);
        await db.SaveChangesAsync();
        var service = new BodyService(db);

        var result = await service.UpsertInbodyRecordAsync(UserId, new UpsertInbodyRecordRequest(
            existing.Id, new DateOnly(2026, 1, 2), 72, null, null, null, null, null, null, null), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteInbodyRecordAsync_RemovesOwnRecord_ReturnsTrue()
    {
        using var db = CreateContext();
        var existing = new InbodyRecord { UserId = UserId, Date = new DateOnly(2026, 1, 1), Weight = 70 };
        db.InbodyRecords.Add(existing);
        await db.SaveChangesAsync();
        var service = new BodyService(db);

        var deleted = await service.DeleteInbodyRecordAsync(UserId, existing.Id, CancellationToken.None);

        Assert.True(deleted);
        Assert.Empty(db.InbodyRecords);
    }

    [Fact]
    public async Task DeleteInbodyRecordAsync_OwnedByAnotherUser_ReturnsFalse()
    {
        using var db = CreateContext();
        var existing = new InbodyRecord { UserId = OtherUserId, Date = new DateOnly(2026, 1, 1), Weight = 70 };
        db.InbodyRecords.Add(existing);
        await db.SaveChangesAsync();
        var service = new BodyService(db);

        var deleted = await service.DeleteInbodyRecordAsync(UserId, existing.Id, CancellationToken.None);

        Assert.False(deleted);
        Assert.Single(db.InbodyRecords);
    }

    private static GymHubDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GymHubDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new GymHubDbContext(options);
    }
}
