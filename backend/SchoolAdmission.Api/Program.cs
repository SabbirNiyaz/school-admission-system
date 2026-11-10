using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Models;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "ConnectionStrings__Default environment variable is not set.");

var holdSeconds = builder.Configuration.GetValue("HOLD_DURATION_SECONDS", 600);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddSingleton(new HoldSettings(Math.Max(1, holdSeconds)));

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod()));

var app = builder.Build();

app.UseCors();

// Log unhandled exceptions and return the standard JSON error shape.
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;

    app.Logger.LogError(ex, "Unhandled error on {Method} {Path}",
        context.Request.Method, context.Request.Path);

    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new ErrorResponse(
        "INTERNAL_ERROR",
        "সার্ভারে সমস্যা হয়েছে, একটু পরে আবার চেষ্টা করুন।"));
}));

// Wait for PostgreSQL and create the schema on first startup.
for (var attempt = 1; ; attempt++)
{
    try
    {
        using var scope = app.Services.CreateScope();

        await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Database.EnsureCreatedAsync();

        break;
    }
    catch (Exception ex) when (attempt < 30)
    {
        Console.WriteLine(
            $"Database not ready yet ({attempt}/30): {ex.Message}");

        await Task.Delay(TimeSpan.FromSeconds(2));
    }
}

// ---------------- Classes ----------------

// Create a class.
app.MapPost("/classes", async (CreateClassRequest body, AppDbContext db) =>
{
    var name = body.Name?.Trim();

    if (string.IsNullOrEmpty(name) || name.Length > 200)
    {
        return Error(
            400,
            "INVALID_NAME",
            "ক্লাসের নাম দিন (সর্বোচ্চ ২০০ অক্ষর)।");
    }

    if (body.TotalSeats is not int seats || seats < 1 || seats > 100_000)
    {
        return Error(
            400,
            "INVALID_TOTAL_SEATS",
            "আসন সংখ্যা ১ থেকে ১,০০,০০০ এর মধ্যে হতে হবে।");
    }

    var cls = new SchoolClass
    {
        Id = NewId("c"),
        Name = name,
        TotalSeats = seats
    };

    db.Classes.Add(cls);

    await db.SaveChangesAsync();

    return Results.Created(
        $"/classes/{cls.Id}",
        new CreatedClassResponse(
            cls.Id,
            cls.Name,
            cls.TotalSeats));
});

// Get all classes, sorted by name.
app.MapGet("/classes", async (AppDbContext db) =>
{
    var rows = await LoadClassRowsAsync(db, DateTimeOffset.UtcNow);

    return Results.Ok(rows.Select(ToClassResponse));
});

// Get a single class.
app.MapGet("/classes/{classId}", async (
    string classId,
    AppDbContext db) =>
{
    var rows = await LoadClassRowsAsync(db, DateTimeOffset.UtcNow, classId);
    var row = rows.FirstOrDefault();

    return row is null
        ? Error(404, "CLASS_NOT_FOUND", "এই ক্লাসটি পাওয়া যায়নি।")
        : Results.Ok(ToClassResponse(row));
});

// ---------------- Holds / applications ----------------

// Create an admission hold.
app.MapPost("/classes/{classId}/holds",
    async (
        string classId,
        HttpRequest http,
        AppDbContext db,
        HoldSettings settings) =>
{
    var studentId = StudentIdFrom(http);

    if (studentId is null)
        return InvalidStudentId();

    await using var tx = await db.Database.BeginTransactionAsync();

    // Lock the class row so concurrent applications for it run one at a time.
    var cls = await LockClassAsync(db, classId);

    if (cls is null)
    {
        return Error(
            404,
            "CLASS_NOT_FOUND",
            "এই ক্লাসটি পাওয়া যায়নি।");
    }

    var now = DateTimeOffset.UtcNow;

    // Expire old holds of this class.
    await db.Holds
        .Where(h =>
            h.ClassId == classId &&
            h.Status == HoldStatus.Held &&
            h.ExpiresAt <= now)
        .ExecuteUpdateAsync(s =>
            s.SetProperty(h => h.Status, HoldStatus.Expired));

    // Check whether the student already has an active application.
    var alreadyActive = await db.Holds.AnyAsync(h =>
        h.ClassId == classId &&
        h.StudentId == studentId &&
        (h.Status == HoldStatus.Held ||
         h.Status == HoldStatus.Confirmed));

    if (alreadyActive)
    {
        return Error(
            409,
            "ALREADY_APPLIED",
            "আপনার এই ক্লাসে ইতিমধ্যে একটি সক্রিয় আবেদন বা ভর্তি আছে।");
    }

    // Count occupied seats.
    var used = await db.Holds.CountAsync(h =>
        h.ClassId == classId &&
        (h.Status == HoldStatus.Confirmed ||
         (h.Status == HoldStatus.Held && h.ExpiresAt > now)));

    if (used >= cls.TotalSeats)
    {
        return Error(
            409,
            "SEAT_FULL",
            "দুঃখিত, এই ক্লাসে আর কোনো আসন খালি নেই।");
    }

    // Create the new hold.
    var hold = new Hold
    {
        Id = NewId("h"),
        ClassId = classId,
        StudentId = studentId,
        Status = HoldStatus.Held,
        CreatedAt = now,
        ExpiresAt = now.AddSeconds(settings.DurationSeconds)
    };

    db.Holds.Add(hold);

    await db.SaveChangesAsync();
    await tx.CommitAsync();

    return Results.Created(
        $"/holds/{hold.Id}",
        new HoldResponse(
            hold.Id,
            hold.ClassId,
            hold.StudentId,
            "HELD",
            ToApiTime(hold.ExpiresAt)));
});

// Get hold status.
app.MapGet("/holds/{holdId}", async (
    string holdId,
    AppDbContext db) =>
{
    var now = DateTimeOffset.UtcNow;

    var h = await db.Holds
        .AsNoTracking()
        .Where(x => x.Id == holdId)
        .Select(x => new
        {
            x.Id,
            x.ClassId,
            x.Status,
            x.ExpiresAt
        })
        .FirstOrDefaultAsync();

    if (h is null)
        return HoldNotFound();

    return Results.Ok(
        new HoldStatusResponse(
            h.Id,
            h.ClassId,
            StatusText(EffectiveStatus(h.Status, h.ExpiresAt, now)),
            ToApiTime(h.ExpiresAt)));
});

// Confirm an admission hold (pay the fee).
app.MapPost("/holds/{holdId}/confirm", async (
    string holdId,
    HttpRequest http,
    AppDbContext db) =>
{
    var studentId = StudentIdFrom(http);

    if (studentId is null)
        return InvalidStudentId();

    var now = DateTimeOffset.UtcNow;

    var updated = await db.Holds
        .Where(h =>
            h.Id == holdId &&
            h.StudentId == studentId &&
            h.Status == HoldStatus.Held &&
            h.ExpiresAt > now)
        .ExecuteUpdateAsync(s =>
            s.SetProperty(h => h.Status, HoldStatus.Confirmed));

    if (updated == 1)
    {
        return Results.Ok(
            new ConfirmResponse(holdId, "CONFIRMED"));
    }

    var hold = await FindOwnHold(db, holdId, studentId);

    if (hold is null)
        return HoldNotFound();

    var status = EffectiveStatus(hold.Status, hold.ExpiresAt, now);

    // Repeat confirm: already confirmed, do not count the seat again.
    if (status == HoldStatus.Confirmed)
    {
        return Results.Ok(
            new ConfirmResponse(holdId, "CONFIRMED"));
    }

    if (status == HoldStatus.Expired)
    {
        return Error(
            409,
            "HOLD_EXPIRED",
            "সময়সীমা শেষ হয়ে গেছে, তাই আবেদনটি আর নিশ্চিত করা যাবে না।");
    }

    return Error(
        409,
        "HOLD_NOT_ACTIVE",
        "এই আবেদনটি আর নিশ্চিত করা যাবে না।");
});

// Cancel an admission hold.
app.MapDelete("/holds/{holdId}", async (
    string holdId,
    HttpRequest http,
    AppDbContext db) =>
{
    var studentId = StudentIdFrom(http);

    if (studentId is null)
        return InvalidStudentId();

    var now = DateTimeOffset.UtcNow;

    var updated = await db.Holds
        .Where(h =>
            h.Id == holdId &&
            h.StudentId == studentId &&
            h.Status == HoldStatus.Held &&
            h.ExpiresAt > now)
        .ExecuteUpdateAsync(s =>
            s.SetProperty(h => h.Status, HoldStatus.Cancelled));

    if (updated == 1)
        return Results.NoContent();

    var hold = await FindOwnHold(db, holdId, studentId);

    if (hold is null)
        return HoldNotFound();

    if (EffectiveStatus(hold.Status, hold.ExpiresAt, now)
        == HoldStatus.Cancelled)
    {
        return Results.NoContent();
    }

    return Error(
        409,
        "HOLD_NOT_CANCELLABLE",
        "এই আবেদনটি আর বাতিল করা যাবে না।");
});

// Get the current student's applications.
app.MapGet("/me/holds", async (
    HttpRequest http,
    AppDbContext db) =>
{
    var studentId = StudentIdFrom(http);

    if (studentId is null)
        return InvalidStudentId();

    var now = DateTimeOffset.UtcNow;

    var rows = await db.Holds
        .AsNoTracking()
        .Where(h => h.StudentId == studentId)
        .OrderByDescending(h => h.CreatedAt)
        .Select(h => new
        {
            h.Id,
            h.ClassId,
            ClassName = h.Class.Name,
            h.Status,
            h.ExpiresAt
        })
        .ToListAsync();

    var responses = rows.Select(r =>
        new MyHoldResponse(
            r.Id,
            r.ClassId,
            r.ClassName,
            StatusText(EffectiveStatus(r.Status, r.ExpiresAt, now)),
            ToApiTime(r.ExpiresAt)));

    return Results.Ok(responses);
});

app.Run();

// ---------------- Helpers ----------------

// Load classes with seat counts.
// The SQL query selects plain columns and counts only.
// ClassRow objects are built in C# after the query runs,
// because EF Core cannot translate a record constructor inside SQL.
static async Task<List<ClassRow>> LoadClassRowsAsync(
    AppDbContext db,
    DateTimeOffset now,
    string? classId = null)
{
    var query = db.Classes.AsNoTracking();

    if (classId is not null)
        query = query.Where(c => c.Id == classId);

    var raw = await query
        .OrderBy(c => c.Name)
        .Select(c => new
        {
            c.Id,
            c.Name,
            c.TotalSeats,
            Held = c.Holds.Count(h =>
                h.Status == HoldStatus.Held &&
                h.ExpiresAt > now),
            Confirmed = c.Holds.Count(h =>
                h.Status == HoldStatus.Confirmed)
        })
        .ToListAsync();

    return raw
        .Select(r => new ClassRow(
            r.Id,
            r.Name,
            r.TotalSeats,
            r.Held,
            r.Confirmed))
        .ToList();
}

// Convert class information into the API response.
static ClassResponse ToClassResponse(ClassRow r) =>
    new(
        r.Id,
        r.Name,
        r.TotalSeats,
        r.HeldSeats,
        r.ConfirmedSeats,
        Math.Max(
            0,
            r.TotalSeats - r.HeldSeats - r.ConfirmedSeats));

// Lock the class row to prevent concurrent seat overbooking.
static async Task<SchoolClass?> LockClassAsync(
    AppDbContext db,
    string classId) =>
    await db.Classes
        .FromSqlInterpolated(
            $"""SELECT * FROM "Classes" WHERE "Id" = {classId} FOR UPDATE""")
        .AsNoTracking()
        .FirstOrDefaultAsync();

// Find a hold that belongs to a specific student.
static async Task<Hold?> FindOwnHold(
    AppDbContext db,
    string holdId,
    string studentId) =>
    await db.Holds
        .AsNoTracking()
        .FirstOrDefaultAsync(h =>
            h.Id == holdId &&
            h.StudentId == studentId);

// Compute the effective hold status at the current time.
static HoldStatus EffectiveStatus(
    HoldStatus stored,
    DateTimeOffset expiresAt,
    DateTimeOffset now) =>
    stored == HoldStatus.Held && expiresAt <= now
        ? HoldStatus.Expired
        : stored;

// Convert the enum to API text, e.g. HELD.
static string StatusText(HoldStatus s) =>
    s.ToString().ToUpperInvariant();

// Convert a time to UTC with whole-second precision.
static DateTime ToApiTime(DateTimeOffset t)
{
    var u = t.UtcDateTime;

    return new DateTime(
        u.Ticks - u.Ticks % TimeSpan.TicksPerSecond,
        DateTimeKind.Utc);
}

// Read and validate the 17-digit student ID from the header.
static string? StudentIdFrom(HttpRequest http)
{
    var v = http.Headers["X-Student-Id"]
        .ToString()
        .Trim();

    return v.Length == 17 && v.All(char.IsDigit)
        ? v
        : null;
}

// Standard API error responses.
static IResult InvalidStudentId() =>
    Error(
        400,
        "INVALID_STUDENT_ID",
        "জন্ম নিবন্ধন নম্বর অবশ্যই ১৭ অঙ্কের হতে হবে।");

static IResult HoldNotFound() =>
    Error(
        404,
        "HOLD_NOT_FOUND",
        "এই আবেদনটি পাওয়া যায়নি।");

static IResult Error(
    int status,
    string code,
    string message) =>
    Results.Json(
        new ErrorResponse(code, message),
        statusCode: status);

// Generate unique IDs with a prefix, e.g. c_ or h_.
static string NewId(string prefix) =>
    $"{prefix}_{Guid.NewGuid():N}";

// Lets the test project reference this app.
public partial class Program { }