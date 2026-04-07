-- SkiShop Seed Data: CouponService — クーポンタイプ・キャンペーン・クーポン
-- 対象DB: coupondb
-- べき等実行: ON CONFLICT (id) DO NOTHING

BEGIN;

-- =============================================================
-- クーポンタイプ（4種）
-- usage_limitation_type: 0=UNLIMITED, 1=SINGLE_USE, 2=MULTI_USE
-- =============================================================
INSERT INTO coupon_types (id, name, description, usage_limitation_type, created_at, updated_at, row_version) VALUES
    ('ctype-welcome',  '新規会員クーポン',   '新規会員登録時に自動付与されるウェルカムクーポン', 1,
     '2026-01-01 00:00:00+09', '2026-01-01 00:00:00+09', '\x'::bytea),
    ('ctype-seasonal', '季節キャンペーン',   'シーズンごとのセールキャンペーン用クーポン',       2,
     '2026-01-01 00:00:00+09', '2026-01-01 00:00:00+09', '\x'::bytea),
    ('ctype-loyalty',  'ロイヤルティクーポン', 'VIP・リピーター向け特別クーポン',               2,
     '2026-01-01 00:00:00+09', '2026-01-01 00:00:00+09', '\x'::bytea),
    ('ctype-flash',    'タイムセール',       '期間限定のフラッシュセール用クーポン',             1,
     '2026-01-01 00:00:00+09', '2026-01-01 00:00:00+09', '\x'::bytea)
ON CONFLICT (id) DO NOTHING;

-- =============================================================
-- キャンペーン（2件）
-- status: 0=DRAFT, 1=ACTIVE, 2=ENDED, 3=CANCELLED
-- =============================================================
INSERT INTO campaigns (id, name, description, status, start_date, end_date, max_coupons, issued_count, created_at, updated_at, row_version) VALUES
    ('camp-spring-2026',
     '2026年春シーズンセール',
     '春スキー・来シーズン準備のための大型セールキャンペーン。スキー板、ブーツ、ウェア等が対象。',
     1,
     '2026-03-01 00:00:00+09', '2026-05-31 23:59:59+09',
     1000, 0,
     '2026-01-15 10:00:00+09', '2026-01-15 10:00:00+09', '\x'::bytea),

    ('camp-new-member',
     '新規会員キャンペーン',
     '新規会員登録でお得なクーポンをプレゼント！初回購入を応援するキャンペーンです。',
     1,
     '2026-01-01 00:00:00+09', '2026-12-31 23:59:59+09',
     5000, 0,
     '2026-01-01 00:00:00+09', '2026-01-01 00:00:00+09', '\x'::bytea)
ON CONFLICT (id) DO NOTHING;

-- =============================================================
-- クーポン（8件）
-- discount_type: 0=PERCENTAGE, 1=FIXED_AMOUNT
-- =============================================================
INSERT INTO coupons (
    id, code, coupon_type_id, campaign_id,
    discount_type, discount_value, max_discount_amount, min_order_amount,
    max_usage_count, current_usage_count, max_usage_per_user,
    valid_from, valid_until, is_active,
    created_at, updated_at, row_version
) VALUES
    -- 1. WELCOME10 — 新規会員10%OFF
    ('coupon-001', 'WELCOME10', 'ctype-welcome', 'camp-new-member',
     0, 10.00, 5000.00, 10000.00,
     5000, 0, 1,
     '2026-01-01 00:00:00+09', '2026-12-31 23:59:59+09', true,
     '2026-01-01 00:00:00+09', '2026-01-01 00:00:00+09', '\x'::bytea),

    -- 2. SPRING2026 — 春セール15%OFF
    ('coupon-002', 'SPRING2026', 'ctype-seasonal', 'camp-spring-2026',
     0, 15.00, 10000.00, 30000.00,
     1000, 0, 3,
     '2026-03-01 00:00:00+09', '2026-05-31 23:59:59+09', true,
     '2026-01-15 10:00:00+09', '2026-01-15 10:00:00+09', '\x'::bytea),

    -- 3. SKISET3000 — スキーセット¥3000OFF
    ('coupon-003', 'SKISET3000', 'ctype-seasonal', 'camp-spring-2026',
     1, 3000.00, NULL, 50000.00,
     500, 0, 1,
     '2026-03-01 00:00:00+09', '2026-05-31 23:59:59+09', true,
     '2026-01-15 10:00:00+09', '2026-01-15 10:00:00+09', '\x'::bytea),

    -- 4. VIP20 — VIP会員20%OFF
    ('coupon-004', 'VIP20', 'ctype-loyalty', NULL,
     0, 20.00, 20000.00, 50000.00,
     200, 0, 2,
     '2026-01-01 00:00:00+09', '2026-12-31 23:59:59+09', true,
     '2026-01-01 00:00:00+09', '2026-01-01 00:00:00+09', '\x'::bytea),

    -- 5. FLASH5000 — タイムセール¥5000OFF
    ('coupon-005', 'FLASH5000', 'ctype-flash', NULL,
     1, 5000.00, NULL, 30000.00,
     100, 0, 1,
     '2026-04-01 00:00:00+09', '2026-04-03 23:59:59+09', true,
     '2026-03-25 10:00:00+09', '2026-03-25 10:00:00+09', '\x'::bytea),

    -- 6. BOOTS15 — ブーツ15%OFF
    ('coupon-006', 'BOOTS15', 'ctype-seasonal', 'camp-spring-2026',
     0, 15.00, 8000.00, 20000.00,
     300, 0, 1,
     '2026-03-01 00:00:00+09', '2026-05-31 23:59:59+09', true,
     '2026-01-15 10:00:00+09', '2026-01-15 10:00:00+09', '\x'::bytea),

    -- 7. NEWYEAR — 新年セール¥2000OFF
    ('coupon-007', 'NEWYEAR', 'ctype-seasonal', NULL,
     1, 2000.00, NULL, 15000.00,
     2000, 0, 1,
     '2026-01-01 00:00:00+09', '2026-01-31 23:59:59+09', true,
     '2026-01-01 00:00:00+09', '2026-01-01 00:00:00+09', '\x'::bytea),

    -- 8. FRIEND500 — お友達紹介¥500OFF
    ('coupon-008', 'FRIEND500', 'ctype-welcome', 'camp-new-member',
     1, 500.00, NULL, 5000.00,
     10000, 0, 5,
     '2026-01-01 00:00:00+09', '2026-12-31 23:59:59+09', true,
     '2026-01-01 00:00:00+09', '2026-01-01 00:00:00+09', '\x'::bytea)

ON CONFLICT (id) DO NOTHING;

COMMIT;
