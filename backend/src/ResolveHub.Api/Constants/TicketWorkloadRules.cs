namespace ResolveHub.Api.Constants;

public static class TicketWorkloadRules
{
    public const int MaxActiveTicketsPerAgent = 5;

    public static readonly string[] ActiveStatuses =
    [
        TicketStatusNames.Assigned,
        TicketStatusNames.InProgress,
        TicketStatusNames.Pending
    ];

    public static string GetCapacityState(int activeTickets) =>
        activeTickets > MaxActiveTicketsPerAgent
            ? "Over Capacity"
            : activeTickets == MaxActiveTicketsPerAgent
                ? "Full"
            : activeTickets == MaxActiveTicketsPerAgent - 1
                ? "Near Capacity"
                : "Available";

    public static int GetRemainingCapacity(int activeTickets) =>
        Math.Max(0, MaxActiveTicketsPerAgent - activeTickets);

    public static bool IsAtCapacity(int activeTickets) =>
        activeTickets >= MaxActiveTicketsPerAgent;
}
