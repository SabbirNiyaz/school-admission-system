namespace SchoolAdmission.Api.Models;

public enum HoldStatus { Held, Confirmed, Expired, Cancelled }

public class SchoolClass
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int TotalSeats { get; set; }
    public List<Hold> Holds { get; set; } = new();
}

public class Hold
{
    public string Id { get; set; } = "";
    public string ClassId { get; set; } = "";
    public SchoolClass Class { get; set; } = null!;
    public string StudentId { get; set; } = "";
    public HoldStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public record HoldSettings(int DurationSeconds);
public record CreateClassRequest(string? Name, int? TotalSeats);
public record ClassRow(string Id, string Name, int TotalSeats, int HeldSeats, int ConfirmedSeats);

public record ClassResponse(
    string Id, string Name, int TotalSeats, int HeldSeats, int ConfirmedSeats, int AvailableSeats);

public record CreatedClassResponse(string Id, string Name, int TotalSeats);
public record HoldResponse(string HoldId, string ClassId, string StudentId, string Status, DateTime ExpiresAt);
public record HoldStatusResponse(string HoldId, string ClassId, string Status, DateTime ExpiresAt);
public record ConfirmResponse(string HoldId, string Status);
public record MyHoldResponse(string HoldId, string ClassId, string ClassName, string Status, DateTime ExpiresAt);
public record ErrorResponse(string Code, string Message);
