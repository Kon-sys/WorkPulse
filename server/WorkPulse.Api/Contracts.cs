namespace WorkPulse.Api;

public sealed record LoginInput(Guid OrganizationId,string Email,string Password);
public sealed record DepartmentInput(string Code,string Name);
public sealed record EmployeeInput(Guid DepartmentId,string EmployeeNumber,string FullName,string? PositionName,DateOnly HiredOn);
public sealed record UserInput(Guid? EmployeeId,string Email,string Password,string Role,Guid? DepartmentId);
public sealed record DeviceInput(Guid EmployeeId,string Name,string Platform);
public sealed record AssignmentInput(string Scope,Guid? TargetId);
public sealed record BreakInput(string Kind);
public sealed record AnalysisInput(DateTimeOffset Until,int SignalLifetimeSeconds);
public sealed record ReviewInput(string Status,string? Comment);
public sealed record EventBatch(IReadOnlyList<WorkPulse.Application.EventInput> Events);

public static class InputValidation
{
    public static string Text(string? value,int maximum=200)
    {
        if(string.IsNullOrWhiteSpace(value) || value.Trim().Length>maximum) throw new WorkPulse.Application.RequestFault(400,$"Текст должен содержать от 1 до {maximum} символов.");
        return value.Trim();
    }
    public static void Password(string? password)
    {
        if(password is null || password.Length is <12 or >128) throw new WorkPulse.Application.RequestFault(400,"Пароль должен содержать от 12 до 128 символов.");
    }
}

public sealed record ResourceInput(string Kind,string Identifier);
