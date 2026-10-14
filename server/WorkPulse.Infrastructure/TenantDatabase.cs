using Npgsql;

namespace WorkPulse.Infrastructure;

/// <summary>Every operation has its own transaction; tenant/actor settings cannot leak through pooled connections.</summary>
public sealed class TenantDatabase(NpgsqlDataSource source)
{
    public async Task<T> Execute<T>(Guid organization, Guid? actor, Func<TenantTransaction, Task<T>> action)
    {
        if (organization == Guid.Empty) throw new ArgumentException("Empty organization.");
        await using var connection = await source.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var db = new TenantTransaction(connection, transaction);
        await db.Query("SELECT set_config('app.organization_id', @org, true), set_config('app.actor_id', @actor, true)",
            ("org", organization.ToString()), ("actor", actor?.ToString() ?? ""));
        var result = await action(db);
        await transaction.CommitAsync();
        return result;
    }
}

public sealed class TenantTransaction(NpgsqlConnection connection, NpgsqlTransaction transaction)
{
    public async Task<List<Dictionary<string, object?>>> Query(string sql, params (string Name, object? Value)[] args)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name,value) in args) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (var i=0;i<reader.FieldCount;i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }
    public async Task<Dictionary<string,object?>?> One(string sql, params (string Name,object? Value)[] args) =>
        (await Query(sql,args)).SingleOrDefault();
}
