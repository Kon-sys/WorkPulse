using System.Security.Cryptography;
using System.Text;
using WorkPulse.Application;

namespace WorkPulse.Infrastructure;

public sealed record LoginRecord(UserAccess Access, string? PasswordHash, DateTime? LockedUntil);

public sealed class IdentityStore(TenantDatabase database)
{
    public Task<LoginRecord?> FindLogin(Guid organization,string email) => database.Execute(organization,null,async db =>
    {
        var row = await db.One("SELECT id,password_hash,locked_until FROM user_accounts WHERE email=@email AND is_active FOR UPDATE",("email",email));
        if (row is null) return null;
        var access=await Load(db,organization,(Guid)row["id"]!);
        return access is null ? null : new LoginRecord(access,(string?)row["password_hash"],(DateTime?)row["locked_until"]);
    });

    public Task<UserAccess?> GetUser(Guid organization,Guid user) => database.Execute(organization,user, db=>Load(db,organization,user));

    private static async Task<UserAccess?> Load(TenantTransaction db,Guid organization,Guid user)
    {
        var row=await db.One("SELECT id,employee_id,email,security_stamp FROM user_accounts WHERE id=@id AND is_active",("id",user));
        if(row is null) return null;
        var grants=await db.Query("SELECT role_code,scope,department_id FROM role_assignments WHERE user_id=@id",("id",user));
        return new(organization,user,(Guid?)row["employee_id"],(string)row["email"]!, (Guid)row["security_stamp"]!,
            grants.Select(g=>new RoleGrant((string)g["role_code"]!,(string)g["scope"]!,(Guid?)g["department_id"])).ToArray());
    }

    public Task<bool> RecordLogin(Guid organization,Guid user,bool success) => database.Execute(organization,null,async db =>
    {
        await db.Query(success
            ? "UPDATE user_accounts SET failed_logins=0,locked_until=NULL WHERE id=@id"
            : "UPDATE user_accounts SET failed_logins=failed_logins+1,locked_until=CASE WHEN failed_logins+1>=5 THEN now()+interval '15 minutes' ELSE locked_until END WHERE id=@id",("id",user));
        return true;
    });

    public Task<DeviceAccess?> AuthenticateDevice(string token)
    {
        var parts=token.Split('.');
        if(parts.Length!=3 || !Guid.TryParse(parts[0],out var organization) || organization==Guid.Empty || !Guid.TryParse(parts[1],out var credential))
            return Task.FromResult<DeviceAccess?>(null);
        return database.Execute<DeviceAccess?>(organization,null,async db=>
        {
            var row=await db.One("SELECT c.token_hash,d.id,d.employee_id FROM device_credentials c JOIN devices d ON d.organization_id=c.organization_id AND d.id=c.device_id WHERE c.id=@id AND c.revoked_at IS NULL AND d.retired_at IS NULL",("id",credential));
            if(row is null || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString((string)row["token_hash"]!),Convert.FromHexString(HashToken(token)))) return null;
            return new DeviceAccess(organization,(Guid)row["id"]!,(Guid)row["employee_id"]!,credential);
        });
    }

    public static string HashToken(string token)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public static string NewToken(Guid organization,Guid credential)=>$"{organization}.{credential}.{Convert.ToHexString(RandomNumberGenerator.GetBytes(32))}";
}
