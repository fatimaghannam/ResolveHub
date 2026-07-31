using ResolveHub.Api.Entities;

namespace ResolveHub.Api.Constants;

public static class TicketCommentRules
{
    public const int MaximumMessageLength = 5000;

    public static bool TryParseVisibility(
        string? value, out CommentVisibility visibility) =>
        Enum.TryParse(value, true, out visibility) &&
        Enum.IsDefined(visibility);

    public static bool IsReadOnly(string statusName) =>
        statusName is TicketStatusNames.Closed or TicketStatusNames.Cancelled or
            TicketStatusNames.Duplicate;

    public static string HistoryDescription(CommentVisibility visibility) =>
        visibility == CommentVisibility.Private
            ? "A Private comment was added."
            : "A Public comment was added.";
}
