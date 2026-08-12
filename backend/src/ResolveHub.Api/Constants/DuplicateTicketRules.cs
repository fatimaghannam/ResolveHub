namespace ResolveHub.Api.Constants;

public static class DuplicateTicketRules //This class contains rules related to duplicate tickets
{
    public const string ReadOnlyMessage =
        "Duplicate tickets are read-only and cannot be modified.";

    public static bool IsDuplicate(string statusName) => //IsDuplicate() checks whether a ticket's status is duplicate or not 
        statusName == TicketStatusNames.Duplicate; //returns true if it is duplicate and false if it is not.
}
