-- SkiShop Seed Data: AuthService — 管理者ユーザー + 10 テスト顧客ユーザー追加
-- 対象DB: authdb
-- 既存の6ロール・12ユーザーに加え、管理者およびテスト用顧客アカウントを投入する。
-- べき等実行: ON CONFLICT DO NOTHING
--
-- ⚠️ 注意: AuthService は ASP.NET Core Identity の PBKDF2 ハッシュを使用するため、
-- SQL シードで直接パスワードハッシュを挿入する場合は AuthService の
-- Pbkdf2PasswordHasher で生成したハッシュ値を使用する必要がある。
-- 推奨: AuthService の POST /api/v1/auth/users でユーザーを登録し、
-- その後 DB で role / status を更新する方法が最も安全。
--
-- Admin Portal ログイン用の管理者アカウントを作成する手順:
-- 1. curl -X POST http://localhost:8080/api/v1/auth/users \
--      -H "Content-Type: application/json" \
--      -d '{"email":"admin@skishop.example.com","username":"admin_skishop","password":"Admin123!","firstName":"管理者","lastName":"SkiShop"}'
-- 2. psql -U skishop -d authdb -c "UPDATE users SET role='ADMIN', status='ACTIVE', email_verified=true WHERE email='admin@skishop.example.com';"
-- 3. psql -U skishop -d authdb -c "INSERT INTO user_roles (id,user_id,role_id,created_at,updated_at) SELECT gen_random_uuid()::text,id,'role-admin',NOW(),NOW() FROM users WHERE email='admin@skishop.example.com' ON CONFLICT DO NOTHING;"

BEGIN;

-- =============================================================
-- 管理者・スタッフユーザー（Admin Portal ログイン用）
-- =============================================================
INSERT INTO users (
    id, email, username, password_hash,
    first_name, last_name,
    status, role, email_verified, is_active, account_locked,
    failed_login_attempts, last_login,
    created_at, updated_at, row_version
) VALUES
    -- admin-001: システム管理者（ADMIN）
    ('admin-seed-001', 'admin@skishop.example.com', 'admin_skishop',
     '$2a$11$8kBbHxY3QFPv0bXRls0S7eKHRVZfMFBw4MwQE8wJqPZOLRjTSVSG.',
     '管理者', 'SkiShop',
     'ACTIVE', 'ADMIN', true, true, false,
     0, NULL,
     '2026-01-01 00:00:00+09', '2026-01-01 00:00:00+09', '\x'),

    -- admin-002: マネージャー（MANAGER）
    ('admin-seed-002', 'manager@skishop.example.com', 'manager_skishop',
     '$2a$11$8kBbHxY3QFPv0bXRls0S7eKHRVZfMFBw4MwQE8wJqPZOLRjTSVSG.',
     '太郎', '管理',
     'ACTIVE', 'MANAGER', true, true, false,
     0, NULL,
     '2026-01-01 00:00:00+09', '2026-01-01 00:00:00+09', '\x'),

    -- admin-003: スタッフ（STAFF）
    ('admin-seed-003', 'staff@skishop.example.com', 'staff_skishop',
     '$2a$11$8kBbHxY3QFPv0bXRls0S7eKHRVZfMFBw4MwQE8wJqPZOLRjTSVSG.',
     '花子', 'スタッフ',
     'ACTIVE', 'STAFF', true, true, false,
     0, NULL,
     '2026-01-01 00:00:00+09', '2026-01-01 00:00:00+09', '\x')

ON CONFLICT (id) DO NOTHING;

-- 管理者ユーザーロール紐付け
INSERT INTO user_roles (id, user_id, role_id, created_at, updated_at) VALUES
    ('ur-admin-001', 'admin-seed-001', 'role-admin',   '2026-01-01 00:00:00+09', '2026-01-01 00:00:00+09'),
    ('ur-admin-002', 'admin-seed-002', 'role-manager', '2026-01-01 00:00:00+09', '2026-01-01 00:00:00+09'),
    ('ur-admin-003', 'admin-seed-003', 'role-staff',   '2026-01-01 00:00:00+09', '2026-01-01 00:00:00+09')
ON CONFLICT (id) DO NOTHING;

-- =============================================================
-- 10 テスト顧客ユーザー（日本人名）
-- =============================================================
INSERT INTO users (
    id, email, username, password_hash,
    first_name, last_name,
    status, role, email_verified, is_active, account_locked,
    failed_login_attempts, last_login,
    created_at, updated_at, row_version
) VALUES
    -- user-seed-001: 田中太郎
    ('user-seed-001', 'tanaka@example.com', 'tanaka_taro',
     '$2a$11$8kBbHxY3QFPv0bXRls0S7eKHRVZfMFBw4MwQE8wJqPZOLRjTSVSG.',
     '太郎', '田中',
     'ACTIVE', 'CUSTOMER', true, true, false,
     0, NULL,
     '2026-01-15 09:00:00+09', '2026-01-15 09:00:00+09', '\x'),

    -- user-seed-002: 佐藤花子
    ('user-seed-002', 'sato@example.com', 'sato_hanako',
     '$2a$11$8kBbHxY3QFPv0bXRls0S7eKHRVZfMFBw4MwQE8wJqPZOLRjTSVSG.',
     '花子', '佐藤',
     'ACTIVE', 'CUSTOMER', true, true, false,
     0, NULL,
     '2026-01-16 10:00:00+09', '2026-01-16 10:00:00+09', '\x'),

    -- user-seed-003: 鈴木一郎
    ('user-seed-003', 'suzuki@example.com', 'suzuki_ichiro',
     '$2a$11$8kBbHxY3QFPv0bXRls0S7eKHRVZfMFBw4MwQE8wJqPZOLRjTSVSG.',
     '一郎', '鈴木',
     'ACTIVE', 'CUSTOMER', true, true, false,
     0, NULL,
     '2026-01-17 11:00:00+09', '2026-01-17 11:00:00+09', '\x'),

    -- user-seed-004: 高橋美咲
    ('user-seed-004', 'takahashi@example.com', 'takahashi_misaki',
     '$2a$11$8kBbHxY3QFPv0bXRls0S7eKHRVZfMFBw4MwQE8wJqPZOLRjTSVSG.',
     '美咲', '高橋',
     'ACTIVE', 'CUSTOMER', true, true, false,
     0, NULL,
     '2026-01-18 12:00:00+09', '2026-01-18 12:00:00+09', '\x'),

    -- user-seed-005: 伊藤健太
    ('user-seed-005', 'ito@example.com', 'ito_kenta',
     '$2a$11$8kBbHxY3QFPv0bXRls0S7eKHRVZfMFBw4MwQE8wJqPZOLRjTSVSG.',
     '健太', '伊藤',
     'ACTIVE', 'CUSTOMER', true, true, false,
     0, NULL,
     '2026-01-19 13:00:00+09', '2026-01-19 13:00:00+09', '\x'),

    -- user-seed-006: 渡辺さくら
    ('user-seed-006', 'watanabe@example.com', 'watanabe_sakura',
     '$2a$11$8kBbHxY3QFPv0bXRls0S7eKHRVZfMFBw4MwQE8wJqPZOLRjTSVSG.',
     'さくら', '渡辺',
     'ACTIVE', 'CUSTOMER', true, true, false,
     0, NULL,
     '2026-01-20 14:00:00+09', '2026-01-20 14:00:00+09', '\x'),

    -- user-seed-007: 山本大輔
    ('user-seed-007', 'yamamoto@example.com', 'yamamoto_daisuke',
     '$2a$11$8kBbHxY3QFPv0bXRls0S7eKHRVZfMFBw4MwQE8wJqPZOLRjTSVSG.',
     '大輔', '山本',
     'ACTIVE', 'CUSTOMER', true, true, false,
     0, NULL,
     '2026-01-21 15:00:00+09', '2026-01-21 15:00:00+09', '\x'),

    -- user-seed-008: 中村優子
    ('user-seed-008', 'nakamura@example.com', 'nakamura_yuko',
     '$2a$11$8kBbHxY3QFPv0bXRls0S7eKHRVZfMFBw4MwQE8wJqPZOLRjTSVSG.',
     '優子', '中村',
     'ACTIVE', 'CUSTOMER', true, true, false,
     0, NULL,
     '2026-01-22 16:00:00+09', '2026-01-22 16:00:00+09', '\x'),

    -- user-seed-009: 小林翔太
    ('user-seed-009', 'kobayashi@example.com', 'kobayashi_shota',
     '$2a$11$8kBbHxY3QFPv0bXRls0S7eKHRVZfMFBw4MwQE8wJqPZOLRjTSVSG.',
     '翔太', '小林',
     'ACTIVE', 'CUSTOMER', true, true, false,
     0, NULL,
     '2026-01-23 17:00:00+09', '2026-01-23 17:00:00+09', '\x'),

    -- user-seed-010: 加藤明日香
    ('user-seed-010', 'kato@example.com', 'kato_asuka',
     '$2a$11$8kBbHxY3QFPv0bXRls0S7eKHRVZfMFBw4MwQE8wJqPZOLRjTSVSG.',
     '明日香', '加藤',
     'ACTIVE', 'CUSTOMER', true, true, false,
     0, NULL,
     '2026-01-24 18:00:00+09', '2026-01-24 18:00:00+09', '\x')

ON CONFLICT (id) DO NOTHING;

-- =============================================================
-- ユーザーロール紐付け（CUSTOMER ロール: role-customer）
-- =============================================================
INSERT INTO user_roles (id, user_id, role_id, created_at, updated_at) VALUES
    ('ur-seed-001', 'user-seed-001', 'role-customer', '2026-01-15 09:00:00+09', '2026-01-15 09:00:00+09'),
    ('ur-seed-002', 'user-seed-002', 'role-customer', '2026-01-16 10:00:00+09', '2026-01-16 10:00:00+09'),
    ('ur-seed-003', 'user-seed-003', 'role-customer', '2026-01-17 11:00:00+09', '2026-01-17 11:00:00+09'),
    ('ur-seed-004', 'user-seed-004', 'role-customer', '2026-01-18 12:00:00+09', '2026-01-18 12:00:00+09'),
    ('ur-seed-005', 'user-seed-005', 'role-customer', '2026-01-19 13:00:00+09', '2026-01-19 13:00:00+09'),
    ('ur-seed-006', 'user-seed-006', 'role-customer', '2026-01-20 14:00:00+09', '2026-01-20 14:00:00+09'),
    ('ur-seed-007', 'user-seed-007', 'role-customer', '2026-01-21 15:00:00+09', '2026-01-21 15:00:00+09'),
    ('ur-seed-008', 'user-seed-008', 'role-customer', '2026-01-22 16:00:00+09', '2026-01-22 16:00:00+09'),
    ('ur-seed-009', 'user-seed-009', 'role-customer', '2026-01-23 17:00:00+09', '2026-01-23 17:00:00+09'),
    ('ur-seed-010', 'user-seed-010', 'role-customer', '2026-01-24 18:00:00+09', '2026-01-24 18:00:00+09')
ON CONFLICT (id) DO NOTHING;

COMMIT;
