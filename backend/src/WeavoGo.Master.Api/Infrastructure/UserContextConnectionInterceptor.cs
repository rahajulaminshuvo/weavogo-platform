using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WeavoGo.Master.Api.Services;

namespace WeavoGo.Master.Api.Infrastructure;

/// <summary>
/// Publishes the acting user id into SQL Server's SESSION_CONTEXT as soon as a
/// connection opens, so the audit triggers in 10_triggers.sql can attribute the
/// rows they write (SDS §8.3.1 — the audit log is written by triggers, never by
/// application code, which is what closes the direct-SQL bypass).
/// </summary>
public sealed class UserContextConnectionInterceptor : DbConnectionInterceptor
{
    private readonly IHttpContextAccessor _accessor;

    public UserContextConnectionInterceptor(IHttpContextAccessor accessor) => _accessor = accessor;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        SetSessionContext(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
                                                     CancellationToken cancellationToken = default)
    {
        await SetSessionContextAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private int? ResolveUserId()
    {
        var claim = _accessor.HttpContext?.User?.FindFirst(Common.ClaimTypesEx.UserId)?.Value;
        return int.TryParse(claim, out var id) && id > 0 ? id : null;
    }

    private void SetSessionContext(DbConnection connection)
    {
        var userId = ResolveUserId();
        if (userId is null) return;

        using var cmd = connection.CreateCommand();
        Configure(cmd, userId.Value);
        cmd.ExecuteNonQuery();
    }

    private async Task SetSessionContextAsync(DbConnection connection, CancellationToken ct)
    {
        var userId = ResolveUserId();
        if (userId is null) return;

        await using var cmd = connection.CreateCommand();
        Configure(cmd, userId.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void Configure(DbCommand cmd, int userId)
    {
        cmd.CommandText = "EXEC sys.sp_set_session_context @key = N'UserId', @value = @userId;";
        var p = cmd.CreateParameter();
        p.ParameterName = "@userId";
        p.Value = userId;
        cmd.Parameters.Add(p);
    }
}
