-- Support for agent-drafted orders awaiting human approval.

-- Concerns found while validating a draft (credit limit, stock shortfall), shown to the approver.
ALTER TABLE erp.orders
    ADD COLUMN review_flags text[] NOT NULL DEFAULT '{}',
    ADD COLUMN idempotency_key text CHECK (idempotency_key ~ '^[A-Za-z0-9._:-]{8,100}$');

-- Lets an agent retry "create draft" safely: the same key always resolves to the same order.
CREATE UNIQUE INDEX ux_orders_idempotency_key ON erp.orders (idempotency_key) WHERE idempotency_key IS NOT NULL;

-- Approvers query this queue constantly; keep it cheap.
CREATE INDEX ix_orders_pending ON erp.orders (created_at) WHERE status = 'pending_approval';
