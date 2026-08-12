namespace ResolveHub.Api.Constants;

public static class TicketWorkloadRules //This class contains the rules for how many tickets an IT agent can handle 
{
    public const int MaxActiveTicketsPerAgent = 5; //an agent can have maximum 5 active tickets

    public static readonly string[] ActiveStatuses = //defines which ticket statuses count as active
    [
        TicketStatusNames.Assigned,
        TicketStatusNames.InProgress,
        TicketStatusNames.Pending
    ];

    public static string GetCapacityState(int activeTickets) => //checks the agent's workload and returns 0-3=> active, 4 tickets : near capacity, 5 tickets--> full , more than 5 is over capacity.
        activeTickets > MaxActiveTicketsPerAgent
            ? "Over Capacity"
            : activeTickets == MaxActiveTicketsPerAgent
                ? "Full"
            : activeTickets == MaxActiveTicketsPerAgent - 1
                ? "Near Capacity"
                : "Available";

    public static int GetRemainingCapacity(int activeTickets) =>
        Math.Max(0, MaxActiveTicketsPerAgent - activeTickets);

    public static bool IsAtCapacity(int activeTickets) => //checks whether the agent has reached the maximum.
        activeTickets >= MaxActiveTicketsPerAgent;
}
