-- Sample wholesale ERP schema.
-- Enumerations use text + CHECK constraints rather than Postgres enums: easier to evolve and
-- maps cleanly to Dapper without custom type handlers.

CREATE SCHEMA IF NOT EXISTS erp;

CREATE TABLE erp.customers (
    id              bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code            text          NOT NULL UNIQUE CHECK (code ~ '^CUST-[0-9]{4}$'),
    name            text          NOT NULL,
    email           text          NOT NULL,
    phone           text,
    city            text          NOT NULL,
    country_code    char(2)       NOT NULL DEFAULT 'GB',
    credit_limit    numeric(12,2) NOT NULL CHECK (credit_limit >= 0),
    account_status  text          NOT NULL DEFAULT 'active'
                                  CHECK (account_status IN ('active', 'on_hold', 'closed')),
    created_at      timestamptz   NOT NULL DEFAULT now()
);

CREATE INDEX ix_customers_name_lower ON erp.customers (lower(name));

CREATE TABLE erp.products (
    id              bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    sku             text          NOT NULL UNIQUE CHECK (sku ~ '^[A-Z]{3}-[0-9]{4}$'),
    name            text          NOT NULL,
    category        text          NOT NULL,
    unit_of_measure text          NOT NULL CHECK (unit_of_measure IN ('each', 'case', 'kg', 'litre', 'pack')),
    unit_price      numeric(12,2) NOT NULL CHECK (unit_price > 0),
    is_active       boolean       NOT NULL DEFAULT true,
    created_at      timestamptz   NOT NULL DEFAULT now()
);

CREATE INDEX ix_products_name_lower ON erp.products (lower(name));
CREATE INDEX ix_products_category ON erp.products (category);

CREATE TABLE erp.warehouses (
    id              bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code            text NOT NULL UNIQUE CHECK (code ~ '^WH-[A-Z]{3}$'),
    name            text NOT NULL,
    city            text NOT NULL
);

CREATE TABLE erp.stock_levels (
    product_id        bigint      NOT NULL REFERENCES erp.products (id),
    warehouse_id      bigint      NOT NULL REFERENCES erp.warehouses (id),
    quantity_on_hand  integer     NOT NULL CHECK (quantity_on_hand >= 0),
    quantity_reserved integer     NOT NULL DEFAULT 0 CHECK (quantity_reserved >= 0),
    reorder_level     integer     NOT NULL DEFAULT 0 CHECK (reorder_level >= 0),
    updated_at        timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (product_id, warehouse_id),
    CHECK (quantity_reserved <= quantity_on_hand)
);

CREATE SEQUENCE erp.order_number_seq START WITH 100001;

CREATE TABLE erp.orders (
    id               bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    order_number     text          NOT NULL UNIQUE
                                   DEFAULT 'SO-' || nextval('erp.order_number_seq')::text,
    customer_id      bigint        NOT NULL REFERENCES erp.customers (id),
    status           text          NOT NULL
                                   CHECK (status IN ('pending_approval', 'approved', 'rejected', 'fulfilled', 'cancelled')),
    source           text          NOT NULL DEFAULT 'manual' CHECK (source IN ('manual', 'agent')),
    created_by       text          NOT NULL,
    created_at       timestamptz   NOT NULL DEFAULT now(),
    decided_by       text,
    decided_at       timestamptz,
    rejection_reason text,
    notes            text,
    total_amount     numeric(12,2) NOT NULL DEFAULT 0 CHECK (total_amount >= 0),
    -- A decision is recorded exactly when the order has left the approval queue.
    CHECK ((status = 'pending_approval') = (decided_at IS NULL)),
    CHECK (status <> 'rejected' OR rejection_reason IS NOT NULL)
);

CREATE INDEX ix_orders_customer_created ON erp.orders (customer_id, created_at DESC);
CREATE INDEX ix_orders_status ON erp.orders (status);

CREATE TABLE erp.order_lines (
    id          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    order_id    bigint        NOT NULL REFERENCES erp.orders (id) ON DELETE CASCADE,
    line_number integer       NOT NULL CHECK (line_number > 0),
    product_id  bigint        NOT NULL REFERENCES erp.products (id),
    quantity    integer       NOT NULL CHECK (quantity > 0),
    unit_price  numeric(12,2) NOT NULL CHECK (unit_price > 0),
    line_total  numeric(12,2) GENERATED ALWAYS AS (quantity * unit_price) STORED,
    UNIQUE (order_id, line_number)
);

CREATE INDEX ix_order_lines_product ON erp.order_lines (product_id);
