-- Append-only audit trail of every MCP tool call and operator command.

CREATE SCHEMA IF NOT EXISTS audit;

CREATE TABLE audit.events (
    id              bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    occurred_at     timestamptz NOT NULL,
    channel         text        NOT NULL CHECK (channel IN ('mcp', 'cli')),
    actor           text        NOT NULL,
    session_id      text        NOT NULL,
    client_name     text,
    client_version  text,
    action          text        NOT NULL,
    arguments       jsonb       NOT NULL DEFAULT '{}',
    outcome         text        NOT NULL
                                CHECK (outcome IN ('success', 'invalid', 'not_found', 'rejected', 'denied', 'failed')),
    error_message   text,
    duration_ms     integer     NOT NULL CHECK (duration_ms >= 0),
    entity_ref      text,
    recorded_at     timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_audit_events_occurred ON audit.events (occurred_at DESC);
CREATE INDEX ix_audit_events_action ON audit.events (action, occurred_at DESC);
CREATE INDEX ix_audit_events_entity ON audit.events (entity_ref) WHERE entity_ref IS NOT NULL;

-- History cannot be rewritten through the application's connection: updates and deletes fail.
-- (A superuser can still disable the trigger; production would also restrict grants and ship
-- events off-box.)
CREATE FUNCTION audit.reject_modification() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'audit.events is append-only (% attempted)', TG_OP
        USING ERRCODE = 'insufficient_privilege';
END;
$$;

CREATE TRIGGER audit_events_append_only
    BEFORE UPDATE OR DELETE OR TRUNCATE ON audit.events
    FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_modification();
