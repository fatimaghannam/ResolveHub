using Microsoft.EntityFrameworkCore;
using ResolveHub.Api.Data;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Entities;
using ResolveHub.Api.Services.Interfaces;

namespace ResolveHub.Api.Services.Implementations;

public sealed class NotificationService(ApplicationDbContext dbContext)
    : INotificationService
{
    public void Add(int recipientUserId, string type, string title, string message,
        string? ticketReference, DateTime createdDate, int? actorUserId = null)
    {
        if (recipientUserId == actorUserId) return;
        dbContext.UserNotifications.Add(new UserNotification
        {
            UserAccountID = recipientUserId,
            Type = type,
            Title = title,
            Message = message,
            TicketReferenceNumber = ticketReference,
            CreatedDate = createdDate
        });
    }

    public async Task<IReadOnlyCollection<UserNotificationDto>> GetAsync(
        int userId, int limit, CancellationToken token) =>
        await dbContext.UserNotifications.AsNoTracking()
            .Where(item => item.UserAccountID == userId)
            .OrderByDescending(item => item.CreatedDate)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(item => new UserNotificationDto(item.ID, item.Type, item.Title,
                item.Message, item.TicketReferenceNumber, item.IsRead, item.CreatedDate))
            .ToListAsync(token);

    public async Task<bool> MarkReadAsync(int userId, int notificationId,
        CancellationToken token)
    {
        var notification = await dbContext.UserNotifications.SingleOrDefaultAsync(
            item => item.ID == notificationId && item.UserAccountID == userId, token);
        if (notification is null) return false;
        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await dbContext.SaveChangesAsync(token);
        }
        return true;
    }

    public async Task MarkAllReadAsync(int userId, CancellationToken token)
    {
        var notifications = await dbContext.UserNotifications
            .Where(item => item.UserAccountID == userId && !item.IsRead).ToListAsync(token);
        foreach (var notification in notifications) notification.IsRead = true;
        if (notifications.Count > 0) await dbContext.SaveChangesAsync(token);
    }
}
