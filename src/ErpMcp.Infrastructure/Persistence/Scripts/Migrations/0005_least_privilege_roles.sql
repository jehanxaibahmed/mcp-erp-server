-- Least-privilege roles, plus support for fail-closed auditing.
--
--   erp_app      what the MCP server and CLI run as: read/write ERP data, INSERT-only audit,
--                no DDL, no access to the migration journals
--   erp_auditor  read-only access to the audit trail
--
-- Both are NOLOGIN group roles. Create a login role that is a member, for example:
--   CREATE ROLE erp_mcp LOGIN PASSWORD '...' IN ROLE erp_app;
-- and point Database:ConnectionString at it, keeping the owner for Database:MigrationConnectionString.

-- Fail-closed auditing writes a 'started' event before a call runs, linked by call_id to its outcome.
ALTER TABLE audit.events ADD COLUMN call_id uuid;
CREATE INDEX ix_audit_events_call ON audit.events (call_id) WHERE call_id IS NOT NULL;

ALTER TABLE audit.events DROP CONSTRAINT events_outcome_check;
ALTER TABLE audit.events ADD CONSTRAINT events_outcome_check
    CHECK (outcome IN ('started', 'success', 'invalid', 'not_found', 'rejected', 'denied', 'failed'));

DO $$
BEGIN
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'erp_app') THEN
            CREATE ROLE erp_app NOLOGIN;
        END IF;
        IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'erp_auditor') THEN
            CREATE ROLE erp_auditor NOLOGIN;
        END IF;
    EXCEPTION WHEN insufficient_privilege THEN
        -- Managed databases often deny CREATEROLE to the migration user. The roles can be
        -- created by an administrator and this script's grants re-applied by hand.
        RAISE NOTICE 'Skipping least-privilege roles: migration user cannot create roles.';
        RETURN;
    END;

    REVOKE ALL ON SCHEMA erp, audit FROM PUBLIC;

    GRANT USAGE ON SCHEMA erp TO erp_app;
    GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA erp TO erp_app;
    GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA erp TO erp_app;
    -- Tables added by later migrations get the same grants automatically.
    ALTER DEFAULT PRIVILEGES IN SCHEMA erp GRANT SELECT, INSERT, UPDATE ON TABLES TO erp_app;
    ALTER DEFAULT PRIVILEGES IN SCHEMA erp GRANT USAGE, SELECT ON SEQUENCES TO erp_app;

    GRANT USAGE ON SCHEMA audit TO erp_app, erp_auditor;
    GRANT INSERT, SELECT ON audit.events TO erp_app;
    GRANT USAGE ON ALL SEQUENCES IN SCHEMA audit TO erp_app;
    GRANT SELECT ON audit.events TO erp_auditor;
END
$$;
