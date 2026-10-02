-- Over HTTP an agent acts for an authenticated person. Record who, on drafts and audit events.
ALTER TABLE erp.orders ADD COLUMN on_behalf_of text;
ALTER TABLE audit.events ADD COLUMN on_behalf_of text;
CREATE INDEX ix_audit_events_on_behalf_of ON audit.events (on_behalf_of, occurred_at DESC) WHERE on_behalf_of IS NOT NULL;
