namespace ResolveHub.Api.Constants;

public static class TicketPendingReasons
{
    public const string EmployeeResponse = "employee-response";
    public const string ManagerApproval = "manager-approval";
    public const string Vendor = "vendor";
    public const string Hardware = "hardware";
    public const string SoftwareLicense = "software-license";
    public const string Other = "other";

    private static readonly IReadOnlyDictionary<string, string> Labels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [EmployeeResponse] = "Waiting for employee response",
            [ManagerApproval] = "Waiting for manager approval",
            [Vendor] = "Waiting for vendor",
            [Hardware] = "Waiting for hardware",
            [SoftwareLicense] = "Waiting for software license",
            [Other] = "Other"
        };

    public static bool TryGetLabel(string? code, out string label) =>
        Labels.TryGetValue(code?.Trim() ?? string.Empty, out label!);
}
