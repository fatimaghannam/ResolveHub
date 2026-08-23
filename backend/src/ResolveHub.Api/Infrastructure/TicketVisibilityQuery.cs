using Microsoft.EntityFrameworkCore;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Entities;

namespace ResolveHub.Api.Infrastructure;

public static class TicketVisibilityQuery
{
    public static IQueryable<Ticket> ReadableBy(
        this IQueryable<Ticket> query, int userId, string role) => role switch
    {
        RoleNames.Employee => query.Where(ticket =>
            !ticket.IsDeleted && ticket.CreatedByUserAccountID == userId),
        RoleNames.ITSupportAgent => query.Where(ticket =>
            !ticket.IsDeleted &&
            (ticket.AssignedToUserAccountID == userId ||
             (ticket.AssignedToUserAccountID == null &&
              ticket.TicketStatus.Name == TicketStatusNames.Open))),
        RoleNames.Manager or RoleNames.Admin => query.Where(ticket => !ticket.IsDeleted),
        _ => query.Where(_ => false)
    };
}
