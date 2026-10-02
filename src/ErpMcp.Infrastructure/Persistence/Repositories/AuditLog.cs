using Dapper;
using ErpMcp.Application.Auditing;
using ErpMcp.Application.Common;
using Npgsql;

namespace ErpMcp.Infrastructure.Persistence.Repositories;

internal sealed class AuditLog(NpgsqlDataSource db) : IAuditLog
{
    public async Task RecordAsync(AuditEntry entry, CancellationToken ct)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO audit.events (occurred_at, channel, actor, session_id, client_name, client_version,
                                      action, arguments, outcome, error_message, duration_ms, entity_ref, call_id, on_behalf_of)
            VALUES (@OccurredAt, @Channel, @Actor, @SessionId, @ClientName, @ClientVersion,
                    @Action, @ArgumentsJson::jsonb, @Outcome, @ErrorMessage, @DurationMs, @EntityRef, @CallId, @OnBehalfOf)
            """,
            new
            {
                OccurredAt = entry.OccurredAt.UtcDateTime,
                entry.Channel,
                entry.Actor,
                entry.SessionId,
                entry.ClientName,
                entry.ClientVersion,
                entry.Action,
                entry.ArgumentsJson,
                Outcome = SnakeCase.From(entry.Outcome),
                entry.ErrorMessage,
                entry.DurationMs,
                entry.EntityRef,
                entry.CallId,
                entry.OnBehalfOf,
            },
            cancellationToken: ct));
    }

    public async Task<IReadOnlyList<AuditRecord>> ListRecentAsync(AuditQuery query, CancellationToken ct)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<AuditRow>(new CommandDefinition("""
            SELECT id AS Id, occurred_at AS OccurredAt, channel AS Channel, actor AS Actor, session_id AS SessionId,
                   client_name AS ClientName, client_version AS ClientVersion, action AS Action,
                   arguments::text AS ArgumentsJson, outcome AS Outcome, error_message AS ErrorMessage,
                   duration_ms AS DurationMs, entity_ref AS EntityRef, call_id AS CallId,
                   on_behalf_of AS OnBehalfOf
            FROM audit.events
            WHERE (@Action::text IS NULL OR action = @Action)
              AND (@Outcome::text IS NULL OR outcome = @Outcome)
              AND (@Actor::text IS NULL OR actor = @Actor OR on_behalf_of = @Actor)
            ORDER BY id DESC
            LIMIT @Limit
            """,
            new
            {
                query.Action,
                Outcome = query.Outcome is null ? null : SnakeCase.From(query.Outcome.Value),
                query.Actor,
                query.Limit,
            },
            cancellationToken: ct));

        return rows.Select(r => r.ToRecord()).ToList();
    }

    private sealed record AuditRow(
        long Id, DateTime OccurredAt, string Channel, string Actor, string SessionId, string? ClientName,
        string? ClientVersion, string Action, string ArgumentsJson, string Outcome, string? ErrorMessage,
        int DurationMs, string? EntityRef, Guid? CallId, string? OnBehalfOf)
    {
        public AuditRecord ToRecord() => new(Id, new AuditEntry(
            Channel, Actor, SessionId, ClientName, ClientVersion, Action, ArgumentsJson,
            SnakeCase.To<AuditOutcome>(Outcome), ErrorMessage, DurationMs, EntityRef, SqlText.AsUtc(OccurredAt))
        {
            CallId = CallId ?? Guid.Empty,
            OnBehalfOf = OnBehalfOf,
        });
    }
}
