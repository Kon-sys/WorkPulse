namespace WorkPulse.Application;

public sealed record RoleGrant(string Role, string Scope, Guid? DepartmentId);
public sealed record UserAccess(Guid OrganizationId, Guid UserId, Guid? EmployeeId,
    string Email, Guid SecurityStamp, IReadOnlyList<RoleGrant> Grants)
{
    public bool IsAdmin => Grants.Any(g => g.Role == "admin" && g.Scope == "organization");
    public bool CanRead(Guid employee, Guid department) => IsAdmin || Grants.Any(g =>
        (g.Role == "hr" && g.Scope == "organization") ||
        (g.Role == "manager" && g.Scope == "department" && g.DepartmentId == department) ||
        (g.Role == "employee" && g.Scope == "self" && EmployeeId == employee));
    public bool CanReview(Guid employee, Guid department) => IsAdmin || Grants.Any(g =>
        (g.Role == "hr" && g.Scope == "organization") ||
        (g.Role == "manager" && g.Scope == "department" && g.DepartmentId == department));
}

public sealed class RequestFault(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}
