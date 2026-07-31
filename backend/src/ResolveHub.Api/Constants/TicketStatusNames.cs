namespace ResolveHub.Api.Constants;

public static class TicketStatusNames
{
    public const string Open = "Open";
    public const string Assigned = "Assigned";
    public const string InProgress = "In Progress";
    public const string Pending = "Pending";
    public const string Resolved = "Resolved";
    public const string Closed = "Closed";
    public const string Cancelled = "Cancelled";
    public const string Duplicate = "Duplicate";

    public static readonly string[] All =
    [
        Open,
        Assigned,
        InProgress,
        Pending,
        Resolved,
        Closed,
        Cancelled,
        Duplicate
    ];
}
