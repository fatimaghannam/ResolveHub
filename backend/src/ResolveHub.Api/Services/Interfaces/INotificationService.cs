using ResolveHub.Api.DTOs.Tickets;

namespace ResolveHub.Api.Services.Interfaces;

public interface INotificationService
{
    void Add(int recipientUserId, string type, string title, string message,
        string? ticketReference, DateTime createdDate, int? actorUserId = null);
    Task<IReadOnlyCollection<UserNotificationDto>> GetAsync(int userId, int limit,
        CancellationToken token);
    Task<bool> MarkReadAsync(int userId, int notificationId, CancellationToken token);
    Task MarkAllReadAsync(int userId, CancellationToken token);
}
