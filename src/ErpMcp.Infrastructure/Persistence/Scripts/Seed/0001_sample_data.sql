-- Synthetic sample data for local development and demos. Every name, email and phone number
-- is fictional. Generated values are deterministic (no random()) so test expectations stay stable.

INSERT INTO erp.warehouses (code, name, city) VALUES
    ('WH-MAN', 'Manchester Distribution Centre', 'Manchester'),
    ('WH-BHM', 'Birmingham Depot',               'Birmingham'),
    ('WH-LDS', 'Leeds Cross-Dock',               'Leeds');

INSERT INTO erp.customers (code, name, email, phone, city, credit_limit, account_status) VALUES
    ('CUST-0001', 'Harbourside Bistro Ltd',        'orders@harbourside-bistro.example',   '0161 496 0001', 'Manchester', 15000, 'active'),
    ('CUST-0002', 'Northgate School Catering',     'kitchen@northgate-school.example',    '0161 496 0002', 'Salford',    40000, 'active'),
    ('CUST-0003', 'The Copper Kettle Cafe',        'hello@copperkettle.example',          '0121 496 0003', 'Birmingham',  5000, 'active'),
    ('CUST-0004', 'Riverside Care Home',           'catering@riverside-care.example',     '0113 496 0004', 'Leeds',      25000, 'active'),
    ('CUST-0005', 'Peak District Hotels Group',    'procurement@peakhotels.example',      '01298 496005',  'Buxton',     60000, 'active'),
    ('CUST-0006', 'Little Lane Bakery',            'bake@littlelane.example',             '0161 496 0006', 'Stockport',   3000, 'on_hold'),
    ('CUST-0007', 'Moorland Golf Club',            'clubhouse@moorlandgolf.example',      '01422 496007',  'Halifax',    12000, 'active'),
    ('CUST-0008', 'City Works Staff Canteen',      'canteen@cityworks.example',           '0121 496 0008', 'Birmingham', 20000, 'active'),
    ('CUST-0009', 'Saffron & Spice Restaurant',    'manager@saffronspice.example',        '0113 496 0009', 'Bradford',    8000, 'active'),
    ('CUST-0010', 'St. Anne''s Community Centre',  'office@stannes-community.example',    '0161 496 0010', 'Oldham',      4000, 'active'),
    ('CUST-0011', 'Greenfield University Halls',   'catering@greenfield-uni.example',     '0113 496 0011', 'Leeds',      75000, 'active'),
    ('CUST-0012', 'The Rusty Anchor Pub',          'landlord@rustyanchor.example',        '0151 496 0012', 'Liverpool',   6000, 'on_hold'),
    ('CUST-0013', 'Brightwater Leisure Centre',    'cafe@brightwater-leisure.example',    '0121 496 0013', 'Wolverhampton', 9000, 'active'),
    ('CUST-0014', 'Oak Tree Nursery',              'admin@oaktree-nursery.example',       '0161 496 0014', 'Bolton',      2500, 'active'),
    ('CUST-0015', 'Pennine Events Catering',       'events@pennine-events.example',       '01484 496015',  'Huddersfield', 30000, 'active'),
    ('CUST-0016', 'Old Mill Deli',                 'deli@oldmill.example',                '01625 496016',  'Macclesfield', 4500, 'closed'),
    ('CUST-0017', 'Westside Hospital Trust',       'supplies@westside-trust.example',     '0121 496 0017', 'Birmingham', 90000, 'active'),
    ('CUST-0018', 'Blue Door Coffee House',        'team@bluedoor.example',               '0113 496 0018', 'Wakefield',   3500, 'active'),
    ('CUST-0019', 'Kingsway Primary Academy',      'kitchen@kingsway-academy.example',    '0161 496 0019', 'Rochdale',   18000, 'active'),
    ('CUST-0020', 'Lakeside Wedding Venue',        'bookings@lakeside-venue.example',     '015394 96020',  'Windermere', 22000, 'active');

INSERT INTO erp.products (sku, name, category, unit_of_measure, unit_price, is_active) VALUES
    ('BEV-0001', 'Ground Coffee Medium Roast 1kg',      'Beverages',   'pack',   14.50, true),
    ('BEV-0002', 'English Breakfast Tea Bags x1100',    'Beverages',   'case',   21.75, true),
    ('BEV-0003', 'Still Spring Water 500ml x24',        'Beverages',   'case',    6.20, true),
    ('BEV-0004', 'Orange Juice Not From Concentrate 1L','Beverages',   'litre',   2.35, true),
    ('BEV-0005', 'Hot Chocolate Powder 2kg',            'Beverages',   'pack',   12.90, true),
    ('DRY-0001', 'Basmati Rice 10kg',                   'Dry Goods',   'pack',   18.40, true),
    ('DRY-0002', 'Penne Pasta 3kg',                     'Dry Goods',   'pack',    5.60, true),
    ('DRY-0003', 'Plain Flour 16kg',                    'Dry Goods',   'pack',   11.25, true),
    ('DRY-0004', 'Granulated Sugar 25kg',               'Dry Goods',   'pack',   24.80, true),
    ('DRY-0005', 'Rolled Oats 3kg',                     'Dry Goods',   'pack',    4.95, true),
    ('DRY-0006', 'Chopped Tomatoes 2.5kg Tin x6',       'Dry Goods',   'case',   13.70, true),
    ('DAI-0001', 'Semi-Skimmed Milk 2L',                'Dairy',       'each',    1.65, true),
    ('DAI-0002', 'Mature Cheddar Block 5kg',            'Dairy',       'each',   32.50, true),
    ('DAI-0003', 'Salted Butter 250g x40',              'Dairy',       'case',   58.00, true),
    ('DAI-0004', 'Greek Style Yoghurt 1kg',             'Dairy',       'each',    3.10, true),
    ('DAI-0005', 'Free Range Eggs x180',                'Dairy',       'case',   36.90, true),
    ('FRZ-0001', 'Frozen Garden Peas 2.5kg',            'Frozen',      'pack',    4.40, true),
    ('FRZ-0002', 'Chunky Oven Chips 2.5kg x4',          'Frozen',      'case',   15.80, true),
    ('FRZ-0003', 'Vanilla Ice Cream 4L',                'Frozen',      'each',    7.95, true),
    ('FRZ-0004', 'Battered Cod Fillets 2kg',            'Frozen',      'pack',   27.60, true),
    ('MEA-0001', 'Chicken Breast Fillets 5kg',          'Meat',        'pack',   34.00, true),
    ('MEA-0002', 'Beef Mince 15% Fat 2kg',              'Meat',        'pack',   16.80, true),
    ('MEA-0003', 'Pork Sausages x60',                   'Meat',        'case',   19.50, true),
    ('MEA-0004', 'Smoked Back Bacon 2kg',               'Meat',        'pack',   18.90, true),
    ('CLN-0001', 'Washing Up Liquid 5L',                'Cleaning',    'each',    6.75, true),
    ('CLN-0002', 'Antibacterial Surface Spray 750ml x6','Cleaning',    'case',    9.30, true),
    ('CLN-0003', 'Blue Centrefeed Roll x6',             'Cleaning',    'case',   16.20, true),
    ('PKG-0001', 'Kraft Takeaway Box Medium x200',      'Packaging',   'case',   22.40, true),
    ('PKG-0002', 'Compostable Coffee Cup 12oz x1000',   'Packaging',   'case',   48.00, true),
    ('PKG-0003', 'Wooden Cutlery Set x500',             'Packaging',   'case',   19.95, false);

-- Stock per product per warehouse. Some rows sit below their reorder level on purpose so the
-- low-stock queries have something to find.
INSERT INTO erp.stock_levels (product_id, warehouse_id, quantity_on_hand, quantity_reserved, reorder_level)
SELECT p.id,
       w.id,
       on_hand,
       LEAST(on_hand, (p.id * 3 + w.id) % 15)   AS reserved,
       20 + (p.id % 4) * 10                     AS reorder_level
FROM erp.products p
CROSS JOIN erp.warehouses w
CROSS JOIN LATERAL (SELECT ((p.id * 37 + w.id * 53) % 240)::integer AS on_hand) s
WHERE p.is_active;

-- Historical orders: 60 orders spread over the last 120 days across active customers.
WITH active_customers AS (
    SELECT id, row_number() OVER (ORDER BY id) AS rn, count(*) OVER () AS total
    FROM erp.customers
    WHERE account_status = 'active'
),
generated AS (
    SELECT g AS n,
           (SELECT id FROM active_customers ac WHERE ac.rn = 1 + (g * 7) % ac.total) AS customer_id,
           now() - make_interval(days => 120 - g * 2, hours => (g * 5) % 24) AS created_at,
           CASE
               WHEN g > 56      THEN 'pending_approval'
               WHEN g % 11 = 0  THEN 'rejected'
               WHEN g % 13 = 0  THEN 'cancelled'
               WHEN g > 50      THEN 'approved'
               ELSE                  'fulfilled'
           END AS status
    FROM generate_series(1, 60) AS g
)
INSERT INTO erp.orders (customer_id, status, source, created_by, created_at,
                        decided_by, decided_at, rejection_reason, notes)
SELECT customer_id,
       status,
       CASE WHEN n % 4 = 0 THEN 'agent' ELSE 'manual' END,
       CASE WHEN n % 4 = 0 THEN 'agent:sample' ELSE 'sales.team@example.com' END,
       created_at,
       CASE WHEN status = 'pending_approval' THEN NULL ELSE 'ops.manager@example.com' END,
       CASE WHEN status = 'pending_approval' THEN NULL ELSE created_at + interval '3 hours' END,
       CASE WHEN status = 'rejected' THEN 'Customer credit limit exceeded' END,
       CASE WHEN n % 9 = 0 THEN 'Deliver before 10am' END
FROM generated
ORDER BY n;

-- 1 to 4 lines per order, priced from the catalogue.
INSERT INTO erp.order_lines (order_id, line_number, product_id, quantity, unit_price)
SELECT o.id,
       l.line_number,
       p.id,
       1 + (o.id * 3 + l.line_number * 7) % 12,
       p.unit_price
FROM erp.orders o
CROSS JOIN LATERAL generate_series(1, 1 + (o.id % 4)::integer) AS l(line_number)
JOIN LATERAL (
    SELECT id, unit_price
    FROM erp.products
    WHERE is_active
    ORDER BY id
    OFFSET ((o.id * 5 + l.line_number * 11) % 29)
    LIMIT 1
) p ON true;

UPDATE erp.orders o
SET total_amount = t.total
FROM (SELECT order_id, sum(line_total) AS total FROM erp.order_lines GROUP BY order_id) t
WHERE t.order_id = o.id;
