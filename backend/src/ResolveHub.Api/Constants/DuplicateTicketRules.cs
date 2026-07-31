namespace ResolveHub.Api.Constants;

public static class DuplicateTicketRules
{
    public const string ReadOnlyMessage =
        "Duplicate tickets are read-only and cannot be modified.";

    public static bool IsDuplicate(string statusName) =>
        statusName == TicketStatusNames.Duplicate;
}
