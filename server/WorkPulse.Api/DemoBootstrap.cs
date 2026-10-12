using Microsoft.AspNetCore.Identity;
using WorkPulse.Infrastructure;

namespace WorkPulse.Api;

public static class DemoBootstrap
{
    public static async Task Run(WebApplication app)
    {
        if(!app.Environment.IsDevelopment()) throw new InvalidOperationException("Demo bootstrap requires Development.");
        var password=Environment.GetEnvironmentVariable("WORKPULSE_DEMO_PASSWORD");InputValidation.Password(password);
        var hasher=app.Services.GetRequiredService<IPasswordHasher<string>>();
        var database=app.Services.GetRequiredService<TenantDatabase>();
        foreach(var org in new[]{Guid.Parse("10000000-0000-0000-0000-000000000001"),Guid.Parse("20000000-0000-0000-0000-000000000001")})
            await database.Execute(org,null,async db=>
            {
                // LR7's employee is already linked to an HR account. Create an independent demo employee.
                var employee=await db.One("INSERT INTO employees(organization_id,department_id,employee_number,full_name,hired_on) VALUES(@org,@org,'API-DEMO','Сотрудник API Демо',current_date) ON CONFLICT(organization_id,employee_number) DO UPDATE SET employee_number=EXCLUDED.employee_number RETURNING id",("org",org));
                foreach(var role in new[]{"admin","hr","manager","employee"})
                {
                    var email=$"{role}@example.test";
                    var user=await db.One("INSERT INTO user_accounts(organization_id,email,employee_id,password_hash) VALUES(@org,@email,@employee::uuid,@hash) ON CONFLICT(organization_id,email) DO NOTHING RETURNING id",("org",org),("email",email),("employee",role=="employee" ? employee!["id"] : null),("hash",hasher.HashPassword(email,password!)));
                    if(user is null) continue;
                    await db.Query("INSERT INTO role_assignments(organization_id,user_id,role_code,scope,department_id) VALUES(@org,@id,@role,@scope,@department::uuid)",
                        ("org",org),("id",user["id"]),("role",role),("scope",role=="employee" ? "self" : role=="manager" ? "department" : "organization"),("department",role=="manager" ? org : null));
                }
                return true;
            });
        Console.WriteLine("Demo users initialized; existing passwords were not reset.");
    }
}
