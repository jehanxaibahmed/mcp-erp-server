-- Stock is reserved when an order is approved, consumed when it is fulfilled and released if
-- an approved order is cancelled.

CREATE TABLE erp.order_allocations (
    order_id     bigint  NOT NULL REFERENCES erp.orders (id) ON DELETE CASCADE,
    line_number  integer NOT NULL,
    product_id   bigint  NOT NULL,
    warehouse_id bigint  NOT NULL,
    quantity     integer NOT NULL CHECK (quantity > 0),
    PRIMARY KEY (order_id, line_number, warehouse_id),
    FOREIGN KEY (order_id, line_number) REFERENCES erp.order_lines (order_id, line_number),
    FOREIGN KEY (product_id, warehouse_id) REFERENCES erp.stock_levels (product_id, warehouse_id)
);

ALTER TABLE erp.orders
    ADD COLUMN closed_by text,
    ADD COLUMN closed_at timestamptz,
    ADD COLUMN cancellation_reason text,
    -- One-directional on purpose: orders that closed before this migration have no closed_at.
    ADD CONSTRAINT ck_orders_closed_state CHECK (closed_at IS NULL OR status IN ('fulfilled', 'cancelled')),
    ADD CONSTRAINT ck_orders_cancellation_reason CHECK (cancellation_reason IS NULL OR status = 'cancelled');
