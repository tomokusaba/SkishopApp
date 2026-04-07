-- SkiShop Seed Data: SalesManagementService — サンプル注文 10件
-- 対象DB: salesdb
-- テスト顧客 user-seed-001〜010 の注文データを投入する。
-- 税率 10%。送料: ¥10,000以上で無料、未満は¥800。
-- べき等実行: ON CONFLICT (id) DO NOTHING

BEGIN;

-- =============================================================
-- 注文（10件）
-- ステータス内訳: DELIVERED×3, SHIPPED×2, CONFIRMED×2, PENDING×1, CANCELLED×1, RETURNED×1
-- =============================================================
INSERT INTO orders (
    id, order_number, customer_id, is_guest, guest_email,
    order_date, status, payment_status, payment_method,
    subtotal_amount, tax_amount, shipping_fee, discount_amount, total_amount,
    coupon_code, used_points, point_discount_amount,
    shipping_postal_code, shipping_prefecture, shipping_city,
    shipping_address_line1, shipping_address_line2,
    shipping_recipient_name, shipping_phone_number,
    currency_code, notes, created_by, updated_by,
    created_at, updated_at, row_version
) VALUES

-- ORD-2026-0001: 田中太郎 — DELIVERED
('ord-seed-001', 'ORD-2026-0001', 'user-seed-001', false, NULL,
 '2026-02-01 10:00:00+09', 'DELIVERED', 'CAPTURED', 'CREDIT_CARD',
 89800.00, 8980.00, 0.00, 0.00, 98780.00,
 NULL, 0, 0.00,
 '150-0001', '東京都', '渋谷区',
 '神宮前3-25-18', 'スカイハイツ神宮前 501',
 '田中 太郎', '090-1234-5001',
 'JPY', NULL, 'system', 'system',
 '2026-02-01 10:00:00+09', '2026-02-08 14:00:00+09', '\x'::bytea),

-- ORD-2026-0002: 佐藤花子 — DELIVERED
('ord-seed-002', 'ORD-2026-0002', 'user-seed-002', false, NULL,
 '2026-02-05 14:30:00+09', 'DELIVERED', 'CAPTURED', 'CREDIT_CARD',
 45800.00, 4580.00, 0.00, 4580.00, 45800.00,
 'WELCOME10', 0, 0.00,
 '160-0022', '東京都', '新宿区',
 '新宿1-12-5', 'パークサイド新宿 302',
 '佐藤 花子', '090-1234-5002',
 'JPY', '初回注文', 'system', 'system',
 '2026-02-05 14:30:00+09', '2026-02-12 11:00:00+09', '\x'::bytea),

-- ORD-2026-0003: 鈴木一郎 — DELIVERED（高額注文）
('ord-seed-003', 'ORD-2026-0003', 'user-seed-003', false, NULL,
 '2026-02-10 09:00:00+09', 'DELIVERED', 'CAPTURED', 'CREDIT_CARD',
 158000.00, 15800.00, 0.00, 3000.00, 170800.00,
 'SKISET3000', 0, 0.00,
 '530-0001', '大阪府', '大阪市北区',
 '梅田2-4-13', 'グランフロント梅田 1205',
 '鈴木 一郎', '090-1234-5003',
 'JPY', 'スキーセット一式', 'system', 'system',
 '2026-02-10 09:00:00+09', '2026-02-18 10:00:00+09', '\x'::bytea),

-- ORD-2026-0004: 高橋美咲 — SHIPPED
('ord-seed-004', 'ORD-2026-0004', 'user-seed-004', false, NULL,
 '2026-03-01 11:00:00+09', 'SHIPPED', 'CAPTURED', 'CREDIT_CARD',
 32800.00, 3280.00, 0.00, 0.00, 36080.00,
 NULL, 0, 0.00,
 '154-0004', '東京都', '世田谷区',
 '太子堂4-1-1', 'キャロットタワー 803',
 '高橋 美咲', '080-9876-5004',
 'JPY', NULL, 'system', 'system',
 '2026-03-01 11:00:00+09', '2026-03-05 09:00:00+09', '\x'::bytea),

-- ORD-2026-0005: 伊藤健太 — SHIPPED
('ord-seed-005', 'ORD-2026-0005', 'user-seed-005', false, NULL,
 '2026-03-05 16:00:00+09', 'SHIPPED', 'CAPTURED', 'CREDIT_CARD',
 67500.00, 6750.00, 0.00, 0.00, 74250.00,
 NULL, 0, 0.00,
 '106-0032', '東京都', '港区',
 '六本木6-10-1', '六本木ヒルズ レジデンス 2501',
 '伊藤 健太', '090-1234-5005',
 'JPY', NULL, 'system', 'system',
 '2026-03-05 16:00:00+09', '2026-03-08 10:00:00+09', '\x'::bytea),

-- ORD-2026-0006: 渡辺さくら — CONFIRMED
('ord-seed-006', 'ORD-2026-0006', 'user-seed-006', false, NULL,
 '2026-03-10 13:00:00+09', 'CONFIRMED', 'CAPTURED', 'CREDIT_CARD',
 24500.00, 2450.00, 0.00, 0.00, 26950.00,
 NULL, 0, 0.00,
 '542-0076', '大阪府', '大阪市中央区',
 '難波5-1-60', 'なんばスカイオ 1102',
 '渡辺 さくら', '080-9876-5006',
 'JPY', NULL, 'system', 'system',
 '2026-03-10 13:00:00+09', '2026-03-10 14:00:00+09', '\x'::bytea),

-- ORD-2026-0007: 山本大輔 — CONFIRMED
('ord-seed-007', 'ORD-2026-0007', 'user-seed-007', false, NULL,
 '2026-03-12 10:00:00+09', 'CONFIRMED', 'CAPTURED', 'CREDIT_CARD',
 55000.00, 5500.00, 0.00, 0.00, 60500.00,
 NULL, 0, 0.00,
 '171-0022', '東京都', '豊島区',
 '南池袋1-28-1', '西武池袋パークタワー 1506',
 '山本 大輔', '090-1234-5007',
 'JPY', NULL, 'system', 'system',
 '2026-03-12 10:00:00+09', '2026-03-12 11:00:00+09', '\x'::bytea),

-- ORD-2026-0008: 中村優子 — PENDING
('ord-seed-008', 'ORD-2026-0008', 'user-seed-008', false, NULL,
 '2026-03-15 15:00:00+09', 'PENDING', 'PENDING', 'CREDIT_CARD',
 8500.00, 850.00, 800.00, 0.00, 10150.00,
 NULL, 0, 0.00,
 '153-0064', '東京都', '目黒区',
 '下目黒3-7-22', 'アトラスタワー目黒 907',
 '中村 優子', '080-9876-5008',
 'JPY', NULL, 'system', 'system',
 '2026-03-15 15:00:00+09', '2026-03-15 15:00:00+09', '\x'::bytea),

-- ORD-2026-0009: 小林翔太 — CANCELLED
('ord-seed-009', 'ORD-2026-0009', 'user-seed-009', false, NULL,
 '2026-03-08 09:00:00+09', 'CANCELLED', 'REFUNDED', 'CREDIT_CARD',
 42000.00, 4200.00, 0.00, 0.00, 46200.00,
 NULL, 0, 0.00,
 '543-0001', '大阪府', '大阪市天王寺区',
 '上本町6-5-13', 'シェラトン都ホテル大阪 405',
 '小林 翔太', '090-1234-5009',
 'JPY', 'お客様都合によるキャンセル', 'system', 'system',
 '2026-03-08 09:00:00+09', '2026-03-09 10:00:00+09', '\x'::bytea),

-- ORD-2026-0010: 加藤明日香 — RETURNED
('ord-seed-010', 'ORD-2026-0010', 'user-seed-010', false, NULL,
 '2026-02-20 12:00:00+09', 'RETURNED', 'REFUNDED', 'CREDIT_CARD',
 125000.00, 12500.00, 0.00, 0.00, 137500.00,
 NULL, 500, 500.00,
 '104-0061', '東京都', '中央区',
 '銀座4-6-16', '銀座三越タワー 1801',
 '加藤 明日香', '080-9876-5010',
 'JPY', 'サイズ交換のため返品', 'system', 'system',
 '2026-02-20 12:00:00+09', '2026-03-05 16:00:00+09', '\x'::bytea)

ON CONFLICT (id) DO NOTHING;

-- =============================================================
-- 注文明細（各注文1〜3アイテム）
-- =============================================================
INSERT INTO order_items (
    id, order_id, product_id, product_name, sku,
    unit_price, quantity, subtotal,
    product_snapshot,
    applied_coupon_id, coupon_discount_amount,
    used_points, point_discount_amount
) VALUES

-- ORD-2026-0001: スキー板 + ブーツ
('oi-seed-001', 'ord-seed-001', 'prod-ski-001', 'オールマウンテンスキー APEX 2026', 'SKI-APEX-170',
 54800.00, 1, 54800.00,
 '{"brand": "SkiShop Original", "size": "170cm"}',
 NULL, 0.00, 0, 0.00),

('oi-seed-002', 'ord-seed-001', 'prod-boot-001', 'ハイパフォーマンスブーツ PRO', 'BOOT-PRO-270',
 35000.00, 1, 35000.00,
 '{"brand": "SkiShop Original", "size": "27.0cm"}',
 NULL, 0.00, 0, 0.00),

-- ORD-2026-0002: ジャケット + ゴーグル
('oi-seed-003', 'ord-seed-002', 'prod-jkt-001', 'GORE-TEX スキージャケット', 'JKT-GTX-M',
 35800.00, 1, 35800.00,
 '{"brand": "SkiShop Original", "size": "M"}',
 'coupon-001', 3580.00, 0, 0.00),

('oi-seed-004', 'ord-seed-002', 'prod-gog-001', 'ワイドビューゴーグル', 'GOG-WIDE-BK',
 10000.00, 1, 10000.00,
 '{"brand": "SkiShop Original", "color": "ブラック"}',
 'coupon-001', 1000.00, 0, 0.00),

-- ORD-2026-0003: フルセット（スキー + ブーツ + ジャケット）
('oi-seed-005', 'ord-seed-003', 'prod-ski-002', 'カービングスキー RAZOR', 'SKI-RAZOR-165',
 72000.00, 1, 72000.00,
 '{"brand": "SkiShop Original", "size": "165cm"}',
 'coupon-003', 1200.00, 0, 0.00),

('oi-seed-006', 'ord-seed-003', 'prod-boot-003', 'ワイドフィットブーツ COMFORT', 'BOOT-CMFT-265',
 42000.00, 1, 42000.00,
 '{"brand": "SkiShop Original", "size": "26.5cm"}',
 'coupon-003', 900.00, 0, 0.00),

('oi-seed-007', 'ord-seed-003', 'prod-jkt-003', '2WAY スキージャケット', 'JKT-2WAY-L',
 44000.00, 1, 44000.00,
 '{"brand": "SkiShop Original", "size": "L"}',
 'coupon-003', 900.00, 0, 0.00),

-- ORD-2026-0004: ブーツ + グローブ
('oi-seed-008', 'ord-seed-004', 'prod-boot-005', 'ライトウェイトブーツ AIR', 'BOOT-AIR-250',
 28000.00, 1, 28000.00,
 '{"brand": "SkiShop Original", "size": "25.0cm"}',
 NULL, 0.00, 0, 0.00),

('oi-seed-009', 'ord-seed-004', 'prod-glove-001', 'タッチスクリーン対応グローブ', 'GLV-TOUCH-M',
 4800.00, 1, 4800.00,
 '{"brand": "SkiShop Original", "size": "M"}',
 NULL, 0.00, 0, 0.00),

-- ORD-2026-0005: スキー板 + ヘルメット
('oi-seed-010', 'ord-seed-005', 'prod-ski-003', 'パウダースキー DEEP', 'SKI-DEEP-180',
 58000.00, 1, 58000.00,
 '{"brand": "SkiShop Original", "size": "180cm"}',
 NULL, 0.00, 0, 0.00),

('oi-seed-011', 'ord-seed-005', 'prod-helm-001', 'MIPS ヘルメット SHIELD', 'HELM-MIPS-M',
 9500.00, 1, 9500.00,
 '{"brand": "SkiShop Original", "size": "M"}',
 NULL, 0.00, 0, 0.00),

-- ORD-2026-0006: ゴーグル + グローブ
('oi-seed-012', 'ord-seed-006', 'prod-gog-003', 'ハイコントラストゴーグル', 'GOG-HICON-WH',
 18500.00, 1, 18500.00,
 '{"brand": "SkiShop Original", "color": "ホワイト"}',
 NULL, 0.00, 0, 0.00),

('oi-seed-013', 'ord-seed-006', 'prod-glove-001', 'タッチスクリーン対応グローブ', 'GLV-TOUCH-S',
 4800.00, 1, 4800.00,
 '{"brand": "SkiShop Original", "size": "S"}',
 NULL, 0.00, 0, 0.00),

('oi-seed-014', 'ord-seed-006', 'prod-gog-001', 'ワイドビューゴーグル', 'GOG-WIDE-RD',
 1200.00, 1, 1200.00,
 '{"brand": "SkiShop Original", "color": "レッド", "note": "替えレンズ"}',
 NULL, 0.00, 0, 0.00),

-- ORD-2026-0007: スキー板 + ブーツ
('oi-seed-015', 'ord-seed-007', 'prod-ski-005', 'ビギナースキー EASY', 'SKI-EASY-160',
 32000.00, 1, 32000.00,
 '{"brand": "SkiShop Original", "size": "160cm"}',
 NULL, 0.00, 0, 0.00),

('oi-seed-016', 'ord-seed-007', 'prod-boot-001', 'ハイパフォーマンスブーツ PRO', 'BOOT-PRO-260',
 23000.00, 1, 23000.00,
 '{"brand": "SkiShop Original", "size": "26.0cm"}',
 NULL, 0.00, 0, 0.00),

-- ORD-2026-0008: グローブのみ（低額注文 → 送料あり）
('oi-seed-017', 'ord-seed-008', 'prod-glove-001', 'タッチスクリーン対応グローブ', 'GLV-TOUCH-L',
 4800.00, 1, 4800.00,
 '{"brand": "SkiShop Original", "size": "L"}',
 NULL, 0.00, 0, 0.00),

('oi-seed-018', 'ord-seed-008', 'prod-gog-001', 'ワイドビューゴーグル（替えレンズ）', 'GOG-WIDE-LENS',
 3700.00, 1, 3700.00,
 '{"brand": "SkiShop Original", "type": "替えレンズ"}',
 NULL, 0.00, 0, 0.00),

-- ORD-2026-0009: スキー板（キャンセル済み）
('oi-seed-019', 'ord-seed-009', 'prod-ski-001', 'オールマウンテンスキー APEX 2026', 'SKI-APEX-175',
 42000.00, 1, 42000.00,
 '{"brand": "SkiShop Original", "size": "175cm"}',
 NULL, 0.00, 0, 0.00),

-- ORD-2026-0010: スキー板 + ブーツ + ジャケット（返品済み）
('oi-seed-020', 'ord-seed-010', 'prod-ski-002', 'カービングスキー RAZOR', 'SKI-RAZOR-170',
 72000.00, 1, 72000.00,
 '{"brand": "SkiShop Original", "size": "170cm"}',
 NULL, 0.00, 250, 250.00),

('oi-seed-021', 'ord-seed-010', 'prod-boot-005', 'ライトウェイトブーツ AIR', 'BOOT-AIR-240',
 28000.00, 1, 28000.00,
 '{"brand": "SkiShop Original", "size": "24.0cm"}',
 NULL, 0.00, 150, 150.00),

('oi-seed-022', 'ord-seed-010', 'prod-jkt-001', 'GORE-TEX スキージャケット', 'JKT-GTX-S',
 25000.00, 1, 25000.00,
 '{"brand": "SkiShop Original", "size": "S"}',
 NULL, 0.00, 100, 100.00)

ON CONFLICT (id) DO NOTHING;

COMMIT;
