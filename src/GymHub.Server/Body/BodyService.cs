using GymHub.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace GymHub.Server.Body;

/// <summary>CRUD for the authenticated user's body measurements and InBody records.</summary>
public sealed class BodyService(GymHubDbContext db)
{
    public async Task<List<BodyMeasurementDto>> GetMeasurementsAsync(int userId, bool chronological, CancellationToken ct)
    {
        var query = db.BodyMeasurements.AsNoTracking().Where(m => m.UserId == userId);
        query = chronological
            ? query.OrderBy(m => m.Date).ThenBy(m => m.Id)
            : query.OrderByDescending(m => m.Date).ThenByDescending(m => m.Id);
        return await query.Select(ProjectMeasurement).ToListAsync(ct);
    }

    /// <summary>Inserts a new measurement, or updates an existing one owned by the user.</summary>
    public async Task<BodyMeasurementDto?> UpsertMeasurementAsync(int userId, UpsertBodyMeasurementRequest request, CancellationToken ct)
    {
        if (request.Id is { } id)
        {
            var existing = await db.BodyMeasurements.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId, ct);
            if (existing is null)
            {
                return null;
            }

            Apply(existing, request);
            await db.SaveChangesAsync(ct);
            return ProjectMeasurement.Compile()(existing);
        }

        var created = new BodyMeasurement { UserId = userId, Date = request.Date, Weight = request.Weight ?? 0 };
        Apply(created, request);
        db.BodyMeasurements.Add(created);
        await db.SaveChangesAsync(ct);
        return ProjectMeasurement.Compile()(created);
    }

    public async Task<bool> DeleteMeasurementAsync(int userId, int id, CancellationToken ct)
    {
        var existing = await db.BodyMeasurements.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId, ct);
        if (existing is null)
        {
            return false;
        }

        db.BodyMeasurements.Remove(existing);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<InbodyRecordDto>> GetInbodyRecordsAsync(int userId, bool chronological, CancellationToken ct)
    {
        var query = db.InbodyRecords.AsNoTracking().Where(r => r.UserId == userId);
        query = chronological
            ? query.OrderBy(r => r.Date).ThenBy(r => r.Id)
            : query.OrderByDescending(r => r.Date).ThenByDescending(r => r.Id);
        return await query.Select(ProjectInbody).ToListAsync(ct);
    }

    public async Task<InbodyRecordDto?> UpsertInbodyRecordAsync(int userId, UpsertInbodyRecordRequest request, CancellationToken ct)
    {
        if (request.Id is { } id)
        {
            var existing = await db.InbodyRecords.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);
            if (existing is null)
            {
                return null;
            }

            Apply(existing, request);
            await db.SaveChangesAsync(ct);
            return ProjectInbody.Compile()(existing);
        }

        var created = new InbodyRecord { UserId = userId, Date = request.Date, Weight = request.Weight ?? 0 };
        Apply(created, request);
        db.InbodyRecords.Add(created);
        await db.SaveChangesAsync(ct);
        return ProjectInbody.Compile()(created);
    }

    public async Task<bool> DeleteInbodyRecordAsync(int userId, int id, CancellationToken ct)
    {
        var existing = await db.InbodyRecords.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);
        if (existing is null)
        {
            return false;
        }

        db.InbodyRecords.Remove(existing);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static void Apply(BodyMeasurement m, UpsertBodyMeasurementRequest request)
    {
        m.Date = request.Date;
        if (request.Weight is { } weight) m.Weight = weight;
        m.Height = request.Height;
        m.Chest = request.Chest;
        m.Waist = request.Waist;
        m.Hip = request.Hip;
        m.Arm = request.Arm;
        m.Thigh = request.Thigh;
        m.Note = request.Note;
    }

    private static void Apply(InbodyRecord r, UpsertInbodyRecordRequest request)
    {
        r.Date = request.Date;
        if (request.Weight is { } weight) r.Weight = weight;
        r.BodyFatPercentage = request.BodyFatPercentage;
        r.MuscleMass = request.MuscleMass;
        r.Bmi = request.Bmi;
        r.BodyWater = request.BodyWater;
        r.Bmr = request.Bmr;
        r.VisceralFat = request.VisceralFat;
        r.Note = request.Note;
    }

    private static readonly System.Linq.Expressions.Expression<Func<BodyMeasurement, BodyMeasurementDto>> ProjectMeasurement = m =>
        new BodyMeasurementDto(m.Id, m.Date, m.Weight, m.Height, m.Chest, m.Waist, m.Hip, m.Arm, m.Thigh, m.Note);

    private static readonly System.Linq.Expressions.Expression<Func<InbodyRecord, InbodyRecordDto>> ProjectInbody = r =>
        new InbodyRecordDto(r.Id, r.Date, r.Weight, r.BodyFatPercentage, r.MuscleMass, r.Bmi, r.BodyWater, r.Bmr, r.VisceralFat, r.Note);
}

public sealed record BodyMeasurementDto(
    int Id, DateOnly Date, double Weight, double? Height, double? Chest, double? Waist, double? Hip, double? Arm, double? Thigh, string? Note);

public sealed record UpsertBodyMeasurementRequest(
    int? Id, DateOnly Date, double? Weight, double? Height, double? Chest, double? Waist, double? Hip, double? Arm, double? Thigh, string? Note);

public sealed record InbodyRecordDto(
    int Id, DateOnly Date, double Weight, double? BodyFatPercentage, double? MuscleMass, double? Bmi, double? BodyWater, double? Bmr, int? VisceralFat, string? Note);

public sealed record UpsertInbodyRecordRequest(
    int? Id, DateOnly Date, double? Weight, double? BodyFatPercentage, double? MuscleMass, double? Bmi, double? BodyWater, double? Bmr, int? VisceralFat, string? Note);
