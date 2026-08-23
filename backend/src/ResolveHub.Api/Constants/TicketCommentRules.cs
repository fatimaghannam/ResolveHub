using ResolveHub.Api.Entities;

namespace ResolveHub.Api.Constants;

public static class TicketCommentRules //This class contains the rules related to comments on tickets
{
    public const int MaximumMessageLength = 5000;

    public static bool TryParseVisibility( //checks the comment visibility entered and converts it into a valid commentVisibility for example it checks whether a comment is public or private
        string? value, out CommentVisibility visibility) =>
        Enum.TryParse(value, true, out visibility) &&
        Enum.IsDefined(visibility);

    public static bool IsReadOnly(string statusName) => //checks whether comments should be read only based on the ticket status , whether it is closed/cancelled or duplicate
        statusName is TicketStatusNames.Closed or TicketStatusNames.Cancelled or
            TicketStatusNames.Duplicate;

    public static string HistoryDescription(CommentVisibility visibility) => //checks the description that will be saved in the ticket history .
        visibility == CommentVisibility.Private
            ? "A Private comment was added."
            : "A Public comment was added.";
}
