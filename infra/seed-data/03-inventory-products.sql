-- =============================================================================
-- SkiShop InventoryManagementService — 商品シードデータ（100 件）
-- Database: inventorydb (PostgreSQL)
--
-- 概要:
--   スキー用品 100 件の商品データと、対応する価格（prices）、
--   在庫（inventories）、商品画像（product_images）データを投入する。
--   実在するスキーメーカーの実際の商品名・ブランドを参考にしたリアルなデータ。
--
-- べき等性:
--   INSERT ... ON CONFLICT (id) DO NOTHING を使用し、再実行しても安全。
--
-- 実行順序: 03（02-inventory-categories の後に実行）
-- =============================================================================

BEGIN;

-- ---------------------------------------------------------------------------
-- 1. products テーブル（100 件）
-- ---------------------------------------------------------------------------

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-ski-001', 'SKU-SKI-001', 'Salomon S/Force Bold', 'オールマウンテン対応のパワフルなスキー板。Edge Amplifier テクノロジー搭載。', 'Salomon', '{"length":"170cm","radius":"15m","level":"中上級"}'::jsonb, ARRAY['スキー板','オールマウンテン','Salomon']::text[], 'cat-ski', 4.8, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-ski-002', 'SKU-SKI-002', 'Atomic Redster X9S', 'FIS 公認レーシングスキー。Servotec テクノロジーによる安定したターン。', 'Atomic', '{"length":"165cm","radius":"13m","level":"上級"}'::jsonb, ARRAY['スキー板','レーシング','Atomic']::text[], 'cat-ski', 4.5, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-ski-003', 'SKU-SKI-003', 'HEAD Supershape e-Speed', '電子制御 EMC テクノロジー搭載の高性能基礎スキー。', 'HEAD', '{"length":"172cm","radius":"14m","level":"中上級"}'::jsonb, ARRAY['スキー板','基礎スキー','HEAD']::text[], 'cat-ski', 4.6, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-ski-004', 'SKU-SKI-004', 'Rossignol Experience 86 Ti', 'チタン合金プレート搭載のオールマウンテンスキー。安定性とレスポンスに優れたモデル。', 'Rossignol', '{"length":"176cm","radius":"16m","level":"中級"}'::jsonb, ARRAY['スキー板','オールマウンテン','Rossignol']::text[], 'cat-ski', 4.3, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-ski-005', 'SKU-SKI-005', 'Volkl Deacon 76', '3D ガラスファイバー構造による軽量ハイパフォーマンスモデル。', 'Volkl', '{"length":"168cm","radius":"14.5m","level":"中級"}'::jsonb, ARRAY['スキー板','カービング','Volkl']::text[], 'cat-ski', 4.2, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-ski-006', 'SKU-SKI-006', 'K2 Mindbender 99Ti', 'バックカントリーからゲレンデまで対応するフリーライドスキー。チタン Y-Beam 構造。', 'K2', '{"length":"177cm","radius":"21m","level":"上級"}'::jsonb, ARRAY['スキー板','フリーライド','K2']::text[], 'cat-ski', 5.1, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-ski-007', 'SKU-SKI-007', 'Blizzard Brahma 88', 'カーボンフリップコアテクノロジー搭載。パウダーからハードパックまで安定。', 'Blizzard', '{"length":"173cm","radius":"17m","level":"中上級"}'::jsonb, ARRAY['スキー板','オールマウンテン','Blizzard']::text[], 'cat-ski', 4.4, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-ski-008', 'SKU-SKI-008', 'Fischer RC4 The Curv DTX', 'ワールドカップテクノロジー搭載の基礎スキー最上位モデル。', 'Fischer', '{"length":"171cm","radius":"14m","level":"上級"}'::jsonb, ARRAY['スキー板','基礎スキー','Fischer']::text[], 'cat-ski', 4.5, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-ski-009', 'SKU-SKI-009', 'Nordica Enforcer 100', 'バランスの取れたフリーライドスキー。あらゆるコンディションで安定したパフォーマンス。', 'Nordica', '{"length":"177cm","radius":"18.5m","level":"中上級"}'::jsonb, ARRAY['スキー板','フリーライド','Nordica']::text[], 'cat-ski', 4.7, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-ski-010', 'SKU-SKI-010', 'Elan Wingman 86 CTi', 'FusionX テクノロジー搭載の高性能オールマウンテンスキー。', 'Elan', '{"length":"170cm","radius":"15.7m","level":"中級"}'::jsonb, ARRAY['スキー板','オールマウンテン','Elan']::text[], 'cat-ski', 4.1, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-ski-011', 'SKU-SKI-011', 'Salomon QST 106', 'パウダー性能に優れたフリーライドスキー。コルクダンパーテクノロジー。', 'Salomon', '{"length":"181cm","radius":"22m","level":"上級"}'::jsonb, ARRAY['スキー板','パウダー','Salomon']::text[], 'cat-ski', 5.3, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-ski-012', 'SKU-SKI-012', 'Atomic Vantage 79 Ti', '初中級者向けオールマウンテンモデル。軽量で操作しやすいプロリンク構造。', 'Atomic', '{"length":"163cm","radius":"13m","level":"初中級"}'::jsonb, ARRAY['スキー板','初心者','Atomic']::text[], 'cat-ski', 3.9, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-ski-013', 'SKU-SKI-013', 'HEAD WC Rebels e-GS RD', 'FIS 公認ジャイアントスラロームレーシングスキー。KERS テクノロジー搭載。', 'HEAD', '{"length":"183cm","radius":"27m","level":"上級"}'::jsonb, ARRAY['スキー板','レーシング','HEAD']::text[], 'cat-ski', 5.0, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-ski-014', 'SKU-SKI-014', 'Rossignol Hero Elite ST Ti', 'ショートターン最適化の基礎スキー。Ti 構造で安定したエッジグリップ。', 'Rossignol', '{"length":"167cm","radius":"12m","level":"上級"}'::jsonb, ARRAY['スキー板','基礎スキー','Rossignol']::text[], 'cat-ski', 4.4, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-ski-015', 'SKU-SKI-015', 'Fischer Ranger 102 FR', 'フリーライド向け軽量スキー。Air Tec TI テクノロジーで機動性抜群。', 'Fischer', '{"length":"178cm","radius":"19m","level":"中上級"}'::jsonb, ARRAY['スキー板','フリーライド','Fischer']::text[], 'cat-ski', 4.6, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-boot-001', 'SKU-BOOT-001', 'Salomon S/PRO Alpha 120', '高精度フィットのレーシングブーツ。カスタムシェルテクノロジー。', 'Salomon', '{"flex":120,"last_width":"98mm","buckles":4}'::jsonb, ARRAY['ブーツ','レーシング','Salomon']::text[], 'cat-boots', 2.1, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-boot-002', 'SKU-BOOT-002', 'Atomic Hawx Ultra 130', 'メモリーフィットテクノロジーで完璧なフィット。軽量プロリンク構造。', 'Atomic', '{"flex":130,"last_width":"98mm","buckles":4}'::jsonb, ARRAY['ブーツ','レーシング','Atomic']::text[], 'cat-boots', 1.95, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-boot-003', 'SKU-BOOT-003', 'Tecnica Mach1 MV 120', 'ミディアムボリュームラストで快適性とパフォーマンスを両立。', 'Tecnica', '{"flex":120,"last_width":"100mm","buckles":4}'::jsonb, ARRAY['ブーツ','基礎','Tecnica']::text[], 'cat-boots', 2.05, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-boot-004', 'SKU-BOOT-004', 'Lange RX 130', 'デュアルコアシェル構造。パワー伝達効率に優れたエキスパートモデル。', 'Lange', '{"flex":130,"last_width":"97mm","buckles":4}'::jsonb, ARRAY['ブーツ','レーシング','Lange']::text[], 'cat-boots', 2.15, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-boot-005', 'SKU-BOOT-005', 'HEAD Raptor WCR 140', 'ワールドカップ仕様のトップモデル。Liquid Fit テクノロジー。', 'HEAD', '{"flex":140,"last_width":"95mm","buckles":4}'::jsonb, ARRAY['ブーツ','レーシング','HEAD']::text[], 'cat-boots', 2.2, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-boot-006', 'SKU-BOOT-006', 'Rossignol Speed 100', '快適性と操作性に優れた中級者向けブーツ。ワイドラスト対応。', 'Rossignol', '{"flex":100,"last_width":"104mm","buckles":4}'::jsonb, ARRAY['ブーツ','中級','Rossignol']::text[], 'cat-boots', 2.0, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-boot-007', 'SKU-BOOT-007', 'Nordica Speedmachine 3 130', 'インフラロックシェル技術で高いパワー伝達。上級向けモデル。', 'Nordica', '{"flex":130,"last_width":"99mm","buckles":4}'::jsonb, ARRAY['ブーツ','上級','Nordica']::text[], 'cat-boots', 2.1, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-boot-008', 'SKU-BOOT-008', 'Fischer RC4 The Curv 130', 'バキュームフィットテクノロジーで足全体を包み込む。', 'Fischer', '{"flex":130,"last_width":"97mm","buckles":4}'::jsonb, ARRAY['ブーツ','レーシング','Fischer']::text[], 'cat-boots', 2.05, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-boot-009', 'SKU-BOOT-009', 'Dalbello Lupo AX 120', '歩行モード搭載のフリーツーリングブーツ。GripWalk ソール対応。', 'Dalbello', '{"flex":120,"last_width":"99mm","buckles":3}'::jsonb, ARRAY['ブーツ','ツアー','Dalbello']::text[], 'cat-boots', 1.85, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-boot-010', 'SKU-BOOT-010', 'Salomon S/PRO Supra BOA 110', 'BOA フィットシステム搭載。ダイヤルで簡単フィット調整。', 'Salomon', '{"flex":110,"last_width":"100mm","buckles":3}'::jsonb, ARRAY['ブーツ','基礎','Salomon']::text[], 'cat-boots', 2.0, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-boot-011', 'SKU-BOOT-011', 'Atomic Hawx Prime 100', '中級者に最適なオールラウンドブーツ。Memory Fit で快適。', 'Atomic', '{"flex":100,"last_width":"100mm","buckles":4}'::jsonb, ARRAY['ブーツ','中級','Atomic']::text[], 'cat-boots', 1.9, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-boot-012', 'SKU-BOOT-012', 'HEAD Edge LYT 100', '軽量設計の快適ブーツ。初中級者向けの扱いやすいフレックス。', 'HEAD', '{"flex":100,"last_width":"102mm","buckles":4}'::jsonb, ARRAY['ブーツ','初中級','HEAD']::text[], 'cat-boots', 1.8, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-boot-013', 'SKU-BOOT-013', 'K2 Recon 120 MV', 'ミディアムボリュームのオールマウンテンブーツ。GripWalk 対応。', 'K2', '{"flex":120,"last_width":"100mm","buckles":4}'::jsonb, ARRAY['ブーツ','オールマウンテン','K2']::text[], 'cat-boots', 2.0, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-boot-014', 'SKU-BOOT-014', 'Scarpa Maestrale RS', '軽量ツアーブーツの最高峰。Carbon Core テクノロジー搭載。', 'Scarpa', '{"flex":120,"last_width":"100mm","buckles":3}'::jsonb, ARRAY['ブーツ','ツアー','Scarpa']::text[], 'cat-boots', 1.6, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-boot-015', 'SKU-BOOT-015', 'Tecnica Cochise 120 DYN', 'フリーライド + ツアー対応のハイブリッドブーツ。歩行モード搭載。', 'Tecnica', '{"flex":120,"last_width":"99mm","buckles":3}'::jsonb, ARRAY['ブーツ','フリーライド','Tecnica']::text[], 'cat-boots', 1.95, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-bind-001', 'SKU-BIND-001', 'Marker Griffon 13 ID', 'フリーライドからゲレンデまで対応。Inter Pivot ヒールピース搭載。', 'Marker', '{"din":"4-13","type":"Alpine"}'::jsonb, ARRAY['ビンディング','フリーライド','Marker']::text[], 'cat-bindings', 1.05, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-bind-002', 'SKU-BIND-002', 'Tyrolia Attack2 14 GW', 'GripWalk 対応オールマウンテンビンディング。高い解放精度。', 'Tyrolia', '{"din":"4-14","type":"Alpine"}'::jsonb, ARRAY['ビンディング','オールマウンテン','Tyrolia']::text[], 'cat-bindings', 0.95, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-bind-003', 'SKU-BIND-003', 'Look Pivot 15 GW', 'レーシングからフリーライドまで対応するメタルビンディング。', 'Look', '{"din":"6-15","type":"Alpine"}'::jsonb, ARRAY['ビンディング','レーシング','Look']::text[], 'cat-bindings', 1.15, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-bind-004', 'SKU-BIND-004', 'Salomon STH2 WTR 13', 'ワールドカップテストで実証された信頼性。幅広いブーツソール対応。', 'Salomon', '{"din":"5-13","type":"Alpine"}'::jsonb, ARRAY['ビンディング','オールマウンテン','Salomon']::text[], 'cat-bindings', 1.0, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-bind-005', 'SKU-BIND-005', 'Atomic Warden MNC 13', 'MNC 対応。あらゆるブーツ規格に対応する万能モデル。', 'Atomic', '{"din":"4-13","type":"Alpine"}'::jsonb, ARRAY['ビンディング','オールマウンテン','Atomic']::text[], 'cat-bindings', 1.05, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-bind-006', 'SKU-BIND-006', 'Marker Kingpin 13', 'ピンテックツアービンディング。ダウンヒル安定性と登行時の軽快さを両立。', 'Marker', '{"din":"6-13","type":"Touring"}'::jsonb, ARRAY['ビンディング','ツアー','Marker']::text[], 'cat-bindings', 0.8, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-bind-007', 'SKU-BIND-007', 'Tyrolia AAAttack2 11 GW', '初中級者向けのコストパフォーマンスに優れたモデル。', 'Tyrolia', '{"din":"3-11","type":"Alpine"}'::jsonb, ARRAY['ビンディング','初中級','Tyrolia']::text[], 'cat-bindings', 0.9, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-bind-008', 'SKU-BIND-008', 'Look SPX 12 GW', '軽量フリーライドビンディング。Full Action トゥピース。', 'Look', '{"din":"4-12","type":"Alpine"}'::jsonb, ARRAY['ビンディング','フリーライド','Look']::text[], 'cat-bindings', 0.85, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-bind-009', 'SKU-BIND-009', 'Salomon Shift MNC 13', 'ピンモードとアルペンモードを切替可能なハイブリッドビンディング。', 'Salomon', '{"din":"6-13","type":"Touring"}'::jsonb, ARRAY['ビンディング','ツアー','Salomon']::text[], 'cat-bindings', 0.95, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-bind-010', 'SKU-BIND-010', 'HEAD Freeflex ST 14', 'レーシング対応の高DINビンディング。ADRENALIN ベースプレート。', 'HEAD', '{"din":"5-14","type":"Alpine"}'::jsonb, ARRAY['ビンディング','レーシング','HEAD']::text[], 'cat-bindings', 1.1, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-pole-001', 'SKU-POLE-001', 'LEKI WCR GS', 'ワールドカップ仕様のカーボンレーシングポール。トリガーSグリップ。', 'LEKI', '{"material":"Carbon","length":"125cm"}'::jsonb, ARRAY['ストック','レーシング','LEKI']::text[], 'cat-poles', 0.4, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-pole-002', 'SKU-POLE-002', 'Black Diamond Traverse', '3段式伸縮ポール。FlickLock Pro 機構搭載。バックカントリーに最適。', 'Black Diamond', '{"material":"Aluminum","length":"105-145cm"}'::jsonb, ARRAY['ストック','ツアー','Black Diamond']::text[], 'cat-poles', 0.55, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-pole-003', 'SKU-POLE-003', 'Salomon Arctic', '軽量アルミシャフトのオールラウンドポール。エルゴノミクスグリップ。', 'Salomon', '{"material":"Aluminum","length":"120cm"}'::jsonb, ARRAY['ストック','基礎','Salomon']::text[], 'cat-poles', 0.45, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-pole-004', 'SKU-POLE-004', 'Atomic Redster Carbon', 'カーボン素材の超軽量レーシングポール。スイングウェイト最小化。', 'Atomic', '{"material":"Carbon","length":"125cm"}'::jsonb, ARRAY['ストック','レーシング','Atomic']::text[], 'cat-poles', 0.35, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-pole-005', 'SKU-POLE-005', 'Komperdell Nationalteam Carbon', 'オーストリアナショナルチーム使用モデル。カーボン100%。', 'Komperdell', '{"material":"Carbon","length":"120cm"}'::jsonb, ARRAY['ストック','レーシング','Komperdell']::text[], 'cat-poles', 0.32, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-gog-001', 'SKU-GOG-001', 'Oakley Flight Deck L', 'リムレスデザインで広い視野。Prizm Snow レンズテクノロジー搭載。', 'Oakley', '{"lens":"Prizm Snow Sapphire","fit":"Large"}'::jsonb, ARRAY['ゴーグル','ハイエンド','Oakley']::text[], 'cat-goggles', 0.22, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-gog-002', 'SKU-GOG-002', 'Smith I/O Mag', 'マグネット式クイックチェンジレンズ。ChromaPop テクノロジー。', 'Smith', '{"lens":"ChromaPop Sun Green Mirror","fit":"Medium-Large"}'::jsonb, ARRAY['ゴーグル','ハイエンド','Smith']::text[], 'cat-goggles', 0.23, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-gog-003', 'SKU-GOG-003', 'POC Orb Clarity', 'Zeiss レンズ搭載のハイパフォーマンスゴーグル。優れた光学性能。', 'POC', '{"lens":"Zeiss Clarity","fit":"Medium"}'::jsonb, ARRAY['ゴーグル','ハイエンド','POC']::text[], 'cat-goggles', 0.2, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-gog-004', 'SKU-GOG-004', 'UVEX Downhill 2100 CV', 'Colorvision テクノロジーでクリアな視界。曇り止めコーティング。', 'UVEX', '{"lens":"Colorvision Green","fit":"Medium"}'::jsonb, ARRAY['ゴーグル','中級','UVEX']::text[], 'cat-goggles', 0.21, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-gog-005', 'SKU-GOG-005', 'Giro Contour RS', 'VIVID レンズ搭載。磁気式レンズ交換で瞬時にレンズ変更可能。', 'Giro', '{"lens":"VIVID Ember","fit":"Medium-Large"}'::jsonb, ARRAY['ゴーグル','ハイエンド','Giro']::text[], 'cat-goggles', 0.22, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-gog-006', 'SKU-GOG-006', 'Oakley Line Miner L', '円筒形レンズ採用。ヘルメットとの高い互換性。Prizm Snow Rose。', 'Oakley', '{"lens":"Prizm Snow Rose","fit":"Large"}'::jsonb, ARRAY['ゴーグル','ハイエンド','Oakley']::text[], 'cat-goggles', 0.21, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-gog-007', 'SKU-GOG-007', 'Smith Squad MAG', 'マグネット式レンズ交換搭載のミッドレンジモデル。', 'Smith', '{"lens":"ChromaPop Storm Rose Flash","fit":"Medium"}'::jsonb, ARRAY['ゴーグル','中級','Smith']::text[], 'cat-goggles', 0.22, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-gog-008', 'SKU-GOG-008', 'Bolle Nevada', 'フォトクロミックレンズ搭載。光に応じて自動調光。', 'Bolle', '{"lens":"Photochromic Vermillon Blue","fit":"Medium"}'::jsonb, ARRAY['ゴーグル','調光','Bolle']::text[], 'cat-goggles', 0.2, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-gog-009', 'SKU-GOG-009', 'SWANS ROVO-MDH-CU/LI', '日本製高品質ゴーグル。日本人の顔型にフィットするジャパンフィット設計。', 'SWANS', '{"lens":"Shadow Mirror x Ultra Light Purple","fit":"Medium"}'::jsonb, ARRAY['ゴーグル','ジャパンフィット','SWANS']::text[], 'cat-goggles', 0.19, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-gog-010', 'SKU-GOG-010', 'Dragon X2s', 'スーパーアンチフォグテクノロジー搭載。スイングロックシステム。', 'Dragon', '{"lens":"Lumalens Green Ion","fit":"Medium-Large"}'::jsonb, ARRAY['ゴーグル','中級','Dragon']::text[], 'cat-goggles', 0.22, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-helm-001', 'SKU-HELM-001', 'Giro Range MIPS', 'MIPS テクノロジー搭載のフリーライドヘルメット。調整式ベンチレーション。', 'Giro', '{"mips":true,"ventilation":"Adjustable"}'::jsonb, ARRAY['ヘルメット','MIPS','Giro']::text[], 'cat-helmets', 0.45, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-helm-002', 'SKU-HELM-002', 'Smith Vantage MIPS', 'Aerocore コンストラクション。最高レベルの保護性能と軽量性。', 'Smith', '{"mips":true,"ventilation":"21 vents"}'::jsonb, ARRAY['ヘルメット','MIPS','Smith']::text[], 'cat-helmets', 0.42, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-helm-003', 'SKU-HELM-003', 'POC Obex MIPS', 'SPIN テクノロジー搭載。回転衝撃からの保護を強化。', 'POC', '{"mips":true,"ventilation":"Adjustable"}'::jsonb, ARRAY['ヘルメット','MIPS','POC']::text[], 'cat-helmets', 0.48, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-helm-004', 'SKU-HELM-004', 'Oakley MOD5 MIPS', 'Oakley ゴーグルと完璧な互換性。BOA フィットシステム。', 'Oakley', '{"mips":true,"ventilation":"Fixed"}'::jsonb, ARRAY['ヘルメット','MIPS','Oakley']::text[], 'cat-helmets', 0.46, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-helm-005', 'SKU-HELM-005', 'UVEX Legend 2.0', 'IAS 3D フィットシステムで完璧なフィット。ドイツ品質。', 'UVEX', '{"mips":false,"ventilation":"Adjustable"}'::jsonb, ARRAY['ヘルメット','スタンダード','UVEX']::text[], 'cat-helmets', 0.44, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-helm-006', 'SKU-HELM-006', 'Sweet Protection Switcher MIPS', 'ハイブリッドコンストラクションのプレミアムヘルメット。', 'Sweet Protection', '{"mips":true,"ventilation":"12 vents"}'::jsonb, ARRAY['ヘルメット','MIPS','Sweet Protection']::text[], 'cat-helmets', 0.4, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-helm-007', 'SKU-HELM-007', 'Salomon MTN Lab', 'バックカントリー向け超軽量。EN 12492 登山規格にも適合。', 'Salomon', '{"mips":false,"ventilation":"Fixed"}'::jsonb, ARRAY['ヘルメット','バックカントリー','Salomon']::text[], 'cat-helmets', 0.35, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-helm-008', 'SKU-HELM-008', 'Atomic Savor AMID', 'AMID 衝撃吸収テクノロジー。360 Fit System。', 'Atomic', '{"mips":false,"ventilation":"Passive"}'::jsonb, ARRAY['ヘルメット','スタンダード','Atomic']::text[], 'cat-helmets', 0.43, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-helm-009', 'SKU-HELM-009', 'HEAD Radar MIPS', 'レーサー向けハードシェルヘルメット。FIS 対応モデル。', 'HEAD', '{"mips":true,"ventilation":"Fixed"}'::jsonb, ARRAY['ヘルメット','レーシング','HEAD']::text[], 'cat-helmets', 0.5, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-helm-010', 'SKU-HELM-010', 'K2 Verdict MIPS', 'K2 Baseline Audio システム対応。音楽を楽しみながら安全に滑走。', 'K2', '{"mips":true,"ventilation":"Passive"}'::jsonb, ARRAY['ヘルメット','MIPS','K2']::text[], 'cat-helmets', 0.47, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-glv-001', 'SKU-GLV-001', 'Hestra Army Leather Heli Ski', '最高級ゴートレザー使用のプレミアムグローブ。防水透湿インサート内蔵。', 'Hestra', '{"material":"Goat Leather","insulation":"Fiberfill"}'::jsonb, ARRAY['グローブ','レザー','Hestra']::text[], 'cat-gloves', 0.25, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-glv-002', 'SKU-GLV-002', 'Black Diamond Guide Glove', 'GORE-TEX 防水メンブレン搭載。PrimaLoft One 中綿で極寒対応。', 'Black Diamond', '{"material":"Goat Leather + Nylon","insulation":"PrimaLoft One"}'::jsonb, ARRAY['グローブ','GORE-TEX','Black Diamond']::text[], 'cat-gloves', 0.28, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-glv-003', 'SKU-GLV-003', 'Outdoor Research Stormtracker', 'スマートフォン操作対応のタッチスクリーンコンパチブル。', 'Outdoor Research', '{"material":"Softshell + Leather","insulation":"EnduraLoft"}'::jsonb, ARRAY['グローブ','タッチスクリーン','Outdoor Research']::text[], 'cat-gloves', 0.18, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-glv-004', 'SKU-GLV-004', 'Hestra Fall Line', 'クラシックレザーグローブのアイコンモデル。耐久性と保温性に優れた3本指。', 'Hestra', '{"material":"Cowhide Leather","insulation":"Polyester"}'::jsonb, ARRAY['グローブ','ミトン','Hestra']::text[], 'cat-gloves', 0.23, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-glv-005', 'SKU-GLV-005', 'Dakine Titan GORE-TEX', 'GORE-TEX インサートで完全防水。内蔵リストガード。', 'Dakine', '{"material":"Nylon + Leather","insulation":"High Loft Synthetic"}'::jsonb, ARRAY['グローブ','GORE-TEX','Dakine']::text[], 'cat-gloves', 0.24, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-glv-006', 'SKU-GLV-006', 'LEKI Griffin S', 'トリガーS システム対応のレーシンググローブ。ポールとの一体感。', 'LEKI', '{"material":"Softshell + Goat Leather","insulation":"PrimaLoft"}'::jsonb, ARRAY['グローブ','レーシング','LEKI']::text[], 'cat-gloves', 0.2, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-glv-007', 'SKU-GLV-007', 'Reusch Pro RC', 'プロ仕様レーシンググローブ。極薄設計でポール操作性を最大化。', 'Reusch', '{"material":"Softshell + Leather","insulation":"Thin"}'::jsonb, ARRAY['グローブ','レーシング','Reusch']::text[], 'cat-gloves', 0.16, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-glv-008', 'SKU-GLV-008', 'POC Palm Comp VPD 2.0', 'VPD プロテクション素材搭載。衝撃保護付きフリーライドグローブ。', 'POC', '{"material":"Softshell + Clarino","insulation":"Thinsulate"}'::jsonb, ARRAY['グローブ','プロテクション','POC']::text[], 'cat-gloves', 0.22, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-glv-009', 'SKU-GLV-009', 'Salomon Force GORE-TEX', 'GORE-TEX + C-Knit テクノロジー。防水性と柔軟性を高次元で両立。', 'Salomon', '{"material":"Polyester + Leather","insulation":"Insulation 80g"}'::jsonb, ARRAY['グローブ','GORE-TEX','Salomon']::text[], 'cat-gloves', 0.21, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-glv-010', 'SKU-GLV-010', 'Therm-ic PowerGloves 3+1', 'バッテリー内蔵ヒーターグローブ。3段階温度調節で極寒でも快適。', 'Therm-ic', '{"material":"Softshell + Leather","insulation":"Heated + PrimaLoft"}'::jsonb, ARRAY['グローブ','ヒーター','Therm-ic']::text[], 'cat-gloves', 0.35, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-jkt-001', 'SKU-JKT-001', 'Arcteryx Rush Jacket', 'GORE-TEX Pro 素材のプレミアムフリーライドジャケット。最高レベルの防水透湿。', 'Arcteryx', '{"waterproof":"GORE-TEX Pro","insulation":"None (Shell)"}'::jsonb, ARRAY['ジャケット','シェル','Arcteryx']::text[], 'cat-jackets', 0.55, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-jkt-002', 'SKU-JKT-002', 'Patagonia PowSlayer Jacket', 'リサイクル GORE-TEX 採用のエコフレンドリーシェルジャケット。', 'Patagonia', '{"waterproof":"GORE-TEX 3L","insulation":"None (Shell)"}'::jsonb, ARRAY['ジャケット','シェル','Patagonia']::text[], 'cat-jackets', 0.52, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-jkt-003', 'SKU-JKT-003', 'The North Face Ceptor Jacket', 'FUTURELIGHT 防水テクノロジー搭載。バックカントリー向け。', 'The North Face', '{"waterproof":"FUTURELIGHT 3L","insulation":"None (Shell)"}'::jsonb, ARRAY['ジャケット','バックカントリー','The North Face']::text[], 'cat-jackets', 0.5, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-jkt-004', 'SKU-JKT-004', 'Salomon Brilliant Jacket', 'AdvancedSkin Dry 搭載のインサレーションジャケット。', 'Salomon', '{"waterproof":"AdvancedSkin Dry 20K","insulation":"100g Synthetic"}'::jsonb, ARRAY['ジャケット','インサレーション','Salomon']::text[], 'cat-jackets', 0.65, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-jkt-005', 'SKU-JKT-005', 'Mammut Stoney HS Jacket', 'DRYtechnology 防水素材。ストレッチ性に優れた快適なスキージャケット。', 'Mammut', '{"waterproof":"DRYtechnology 20K","insulation":"60g Synthetic"}'::jsonb, ARRAY['ジャケット','オールラウンド','Mammut']::text[], 'cat-jackets', 0.58, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-jkt-006', 'SKU-JKT-006', 'Helly Hansen Alpha 4.0 Jacket', 'Helly Tech Professional 搭載。LifaPow 中綿で軽量高保温。', 'Helly Hansen', '{"waterproof":"Helly Tech Professional","insulation":"LifaPow 80g"}'::jsonb, ARRAY['ジャケット','インサレーション','Helly Hansen']::text[], 'cat-jackets', 0.6, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-jkt-007', 'SKU-JKT-007', 'Norrona Lofoten GORE-TEX Pro Jacket', 'ノルウェー発の最高峰フリーライドジャケット。耐久性と機能性。', 'Norrona', '{"waterproof":"GORE-TEX Pro","insulation":"None (Shell)"}'::jsonb, ARRAY['ジャケット','フリーライド','Norrona']::text[], 'cat-jackets', 0.54, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-jkt-008', 'SKU-JKT-008', 'Descente Swiss Ski Team Jacket', 'スイスナショナルチーム公式。D-LASER CUT 技術。', 'Descente', '{"waterproof":"Dermizax 20K","insulation":"Heat Navi 80g"}'::jsonb, ARRAY['ジャケット','レーシング','Descente']::text[], 'cat-jackets', 0.62, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-jkt-009', 'SKU-JKT-009', 'Phenix Twinpeaks Jacket', '日本ブランドの高機能ジャケット。2Way ストレッチ素材。', 'Phenix', '{"waterproof":"Dermizax 20K","insulation":"Thinsulate 100g"}'::jsonb, ARRAY['ジャケット','オールラウンド','Phenix']::text[], 'cat-jackets', 0.58, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-jkt-010', 'SKU-JKT-010', 'Goldwin G-Bliss Jacket', '日本製プレミアムスキーウェア。GORE-TEX 2L + 光電子中綿。', 'Goldwin', '{"waterproof":"GORE-TEX 2L","insulation":"Kodenshi 80g"}'::jsonb, ARRAY['ジャケット','プレミアム','Goldwin']::text[], 'cat-jackets', 0.6, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-pnt-001', 'SKU-PNT-001', 'Arcteryx Sabre AR Pant', 'GORE-TEX 素材のプレミアムシェルパンツ。立体裁断でスキー動作に最適化。', 'Arcteryx', '{"waterproof":"GORE-TEX","insulation":"None (Shell)"}'::jsonb, ARRAY['パンツ','シェル','Arcteryx']::text[], 'cat-pants', 0.48, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-pnt-002', 'SKU-PNT-002', 'Salomon Stance 3L Pant', '3 レイヤー構造の防水透湿パンツ。AdvancedSkin Dry テクノロジー。', 'Salomon', '{"waterproof":"AdvancedSkin Dry 20K","insulation":"None (Shell)"}'::jsonb, ARRAY['パンツ','シェル','Salomon']::text[], 'cat-pants', 0.45, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-pnt-003', 'SKU-PNT-003', 'The North Face Freedom Insulated Pant', 'Heatseeker エコ中綿搭載のインサレーションパンツ。', 'The North Face', '{"waterproof":"DryVent 2L","insulation":"Heatseeker Eco 60g"}'::jsonb, ARRAY['パンツ','インサレーション','The North Face']::text[], 'cat-pants', 0.55, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-pnt-004', 'SKU-PNT-004', 'Patagonia Insulated Powder Town Pants', 'リサイクル素材使用。H2No 防水性能。環境配慮型。', 'Patagonia', '{"waterproof":"H2No 2L","insulation":"Thermogreen 60g"}'::jsonb, ARRAY['パンツ','インサレーション','Patagonia']::text[], 'cat-pants', 0.52, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-pnt-005', 'SKU-PNT-005', 'Helly Hansen Legendary Insulated Pant', 'LifaPow 中綿搭載のベストセラーモデル。', 'Helly Hansen', '{"waterproof":"Helly Tech Professional","insulation":"PrimaLoft 60g"}'::jsonb, ARRAY['パンツ','インサレーション','Helly Hansen']::text[], 'cat-pants', 0.5, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-pnt-006', 'SKU-PNT-006', 'Mammut Haldigrat HS Pants', 'DRYtechnology 搭載。4Way ストレッチで動きやすい。', 'Mammut', '{"waterproof":"DRYtechnology 20K","insulation":"40g Synthetic"}'::jsonb, ARRAY['パンツ','オールラウンド','Mammut']::text[], 'cat-pants', 0.46, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-pnt-007', 'SKU-PNT-007', 'Descente Swiss Team Pants', 'スイスチーム公式パンツ。レーシング向けスリムフィット。', 'Descente', '{"waterproof":"Dermizax 20K","insulation":"Heat Navi 60g"}'::jsonb, ARRAY['パンツ','レーシング','Descente']::text[], 'cat-pants', 0.48, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-pnt-008', 'SKU-PNT-008', 'Phenix Blizzard Pants', '日本ブランドの高機能パンツ。4Way ストレッチ + 防水透湿。', 'Phenix', '{"waterproof":"Dermizax 20K","insulation":"Thinsulate 80g"}'::jsonb, ARRAY['パンツ','オールラウンド','Phenix']::text[], 'cat-pants', 0.5, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-pnt-009', 'SKU-PNT-009', 'Goldwin G-Bliss Pants', '日本製プレミアム。光電子中綿で遠赤外線効果の保温性。', 'Goldwin', '{"waterproof":"GORE-TEX 2L","insulation":"Kodenshi 60g"}'::jsonb, ARRAY['パンツ','プレミアム','Goldwin']::text[], 'cat-pants', 0.52, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-pnt-010', 'SKU-PNT-010', 'Norrona Lofoten GORE-TEX Pro Pants', '最高峰フリーライドパンツ。GORE-TEX Pro 3L 究極の耐久性。', 'Norrona', '{"waterproof":"GORE-TEX Pro 3L","insulation":"None (Shell)"}'::jsonb, ARRAY['パンツ','フリーライド','Norrona']::text[], 'cat-pants', 0.5, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-acc-001', 'SKU-ACC-001', 'TOKO All-in-One Hot Wax', '全雪質対応のユニバーサルホットワックス。簡単に塗布でき滑走性能を向上。', 'TOKO', '{"type":"Hot Wax","temperature":"-30C to +10C"}'::jsonb, ARRAY['アクセサリー','ワックス','TOKO']::text[], 'cat-accessories', 0.15, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-acc-002', 'SKU-ACC-002', 'GALLIUM Extra Base Violet', '日本製高品質ワックス。低温雪に最適なフッ素入りベースワックス。', 'GALLIUM', '{"type":"Base Wax","temperature":"-12C to -3C"}'::jsonb, ARRAY['アクセサリー','ワックス','GALLIUM']::text[], 'cat-accessories', 0.1, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-acc-003', 'SKU-ACC-003', 'Deuter Freerider 30', 'スキー取り付け可能なバックカントリーバックパック。ヘルメットホルダー付き。', 'Deuter', '{"capacity":"30L","ski_carry":true}'::jsonb, ARRAY['アクセサリー','バックパック','Deuter']::text[], 'cat-accessories', 1.2, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-acc-004', 'SKU-ACC-004', 'Buff Thermonet Neckwarmer', '超軽量ネックウォーマー。Polygiene 防臭加工で長時間快適。', 'Buff', '{"material":"Thermonet","weight":"32g"}'::jsonb, ARRAY['アクセサリー','ネックウォーマー','Buff']::text[], 'cat-accessories', 0.05, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO products (id, sku, name, description, brand, attributes, tags, category_id, weight, active, created_at, updated_at, row_version)
VALUES ('prod-acc-005', 'SKU-ACC-005', 'Swix T76-2 Waxing Iron', 'デジタル温度コントロール付きワクシングアイロン。均一加熱。', 'Swix', '{"type":"Waxing Iron","temperature_range":"100-170C"}'::jsonb, ARRAY['アクセサリー','チューンナップ','Swix']::text[], 'cat-accessories', 0.8, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

-- ---------------------------------------------------------------------------
-- 2. prices テーブル（各商品に 1 価格 = 100 件）
-- ---------------------------------------------------------------------------

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-ski-001', 'prod-ski-001', 89800, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-ski-002', 'prod-ski-002', 128000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-ski-003', 'prod-ski-003', 112000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-ski-004', 'prod-ski-004', 78000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-ski-005', 'prod-ski-005', 82000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-ski-006', 'prod-ski-006', 95000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-ski-007', 'prod-ski-007', 87000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-ski-008', 'prod-ski-008', 118000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-ski-009', 'prod-ski-009', 92000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-ski-010', 'prod-ski-010', 76000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-ski-011', 'prod-ski-011', 98000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-ski-012', 'prod-ski-012', 62000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-ski-013', 'prod-ski-013', 145000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-ski-014', 'prod-ski-014', 105000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-ski-015', 'prod-ski-015', 88000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-boot-001', 'prod-boot-001', 68000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-boot-002', 'prod-boot-002', 72000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-boot-003', 'prod-boot-003', 65000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-boot-004', 'prod-boot-004', 75000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-boot-005', 'prod-boot-005', 88000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-boot-006', 'prod-boot-006', 45000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-boot-007', 'prod-boot-007', 69000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-boot-008', 'prod-boot-008', 78000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-boot-009', 'prod-boot-009', 72000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-boot-010', 'prod-boot-010', 58000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-boot-011', 'prod-boot-011', 48000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-boot-012', 'prod-boot-012', 42000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-boot-013', 'prod-boot-013', 62000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-boot-014', 'prod-boot-014', 85000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-boot-015', 'prod-boot-015', 72000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-bind-001', 'prod-bind-001', 38000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-bind-002', 'prod-bind-002', 35000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-bind-003', 'prod-bind-003', 42000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-bind-004', 'prod-bind-004', 32000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-bind-005', 'prod-bind-005', 34000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-bind-006', 'prod-bind-006', 55000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-bind-007', 'prod-bind-007', 25000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-bind-008', 'prod-bind-008', 36000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-bind-009', 'prod-bind-009', 62000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-bind-010', 'prod-bind-010', 40000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-pole-001', 'prod-pole-001', 22000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-pole-002', 'prod-pole-002', 15000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-pole-003', 'prod-pole-003', 6800, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-pole-004', 'prod-pole-004', 18000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-pole-005', 'prod-pole-005', 25000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-gog-001', 'prod-gog-001', 28000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-gog-002', 'prod-gog-002', 35000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-gog-003', 'prod-gog-003', 32000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-gog-004', 'prod-gog-004', 18000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-gog-005', 'prod-gog-005', 30000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-gog-006', 'prod-gog-006', 22000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-gog-007', 'prod-gog-007', 25000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-gog-008', 'prod-gog-008', 20000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-gog-009', 'prod-gog-009', 16000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-gog-010', 'prod-gog-010', 24000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-helm-001', 'prod-helm-001', 32000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-helm-002', 'prod-helm-002', 35000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-helm-003', 'prod-helm-003', 28000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-helm-004', 'prod-helm-004', 30000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-helm-005', 'prod-helm-005', 18000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-helm-006', 'prod-helm-006', 38000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-helm-007', 'prod-helm-007', 25000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-helm-008', 'prod-helm-008', 22000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-helm-009', 'prod-helm-009', 26000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-helm-010', 'prod-helm-010', 24000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-glv-001', 'prod-glv-001', 22000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-glv-002', 'prod-glv-002', 25000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-glv-003', 'prod-glv-003', 12000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-glv-004', 'prod-glv-004', 18000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-glv-005', 'prod-glv-005', 14000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-glv-006', 'prod-glv-006', 16000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-glv-007', 'prod-glv-007', 13000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-glv-008', 'prod-glv-008', 15000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-glv-009', 'prod-glv-009', 11000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-glv-010', 'prod-glv-010', 35000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-jkt-001', 'prod-jkt-001', 98000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-jkt-002', 'prod-jkt-002', 82000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-jkt-003', 'prod-jkt-003', 68000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-jkt-004', 'prod-jkt-004', 42000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-jkt-005', 'prod-jkt-005', 55000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-jkt-006', 'prod-jkt-006', 62000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-jkt-007', 'prod-jkt-007', 95000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-jkt-008', 'prod-jkt-008', 75000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-jkt-009', 'prod-jkt-009', 48000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-jkt-010', 'prod-jkt-010', 88000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-pnt-001', 'prod-pnt-001', 72000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-pnt-002', 'prod-pnt-002', 38000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-pnt-003', 'prod-pnt-003', 32000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-pnt-004', 'prod-pnt-004', 42000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-pnt-005', 'prod-pnt-005', 35000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-pnt-006', 'prod-pnt-006', 45000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-pnt-007', 'prod-pnt-007', 58000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-pnt-008', 'prod-pnt-008', 38000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-pnt-009', 'prod-pnt-009', 68000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-pnt-010', 'prod-pnt-010', 78000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-acc-001', 'prod-acc-001', 2800, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-acc-002', 'prod-acc-002', 1800, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-acc-003', 'prod-acc-003', 22000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-acc-004', 'prod-acc-004', 3500, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO prices (id, product_id, regular_price, sale_price, sale_start_date, sale_end_date, currency_code, is_active, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('price-acc-005', 'prod-acc-005', 12000, NULL, NULL, NULL, 'JPY', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

-- ---------------------------------------------------------------------------
-- 3. inventories テーブル（各商品に 1 在庫 = 100 件）
-- ---------------------------------------------------------------------------

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-ski-001', 'prod-ski-001', 33, 0, 'WH-TOKYO-01', 'IN_STOCK', 6, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-ski-002', 'prod-ski-002', 11, 0, 'WH-OSAKA-01', 'IN_STOCK', 5, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-ski-003', 'prod-ski-003', 75, 0, 'WH-NAGANO-01', 'IN_STOCK', 15, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-ski-004', 'prod-ski-004', 67, 0, 'WH-TOKYO-01', 'IN_STOCK', 13, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-ski-005', 'prod-ski-005', 62, 0, 'WH-OSAKA-01', 'IN_STOCK', 12, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-ski-006', 'prod-ski-006', 40, 0, 'WH-NAGANO-01', 'IN_STOCK', 8, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-ski-007', 'prod-ski-007', 31, 0, 'WH-TOKYO-01', 'IN_STOCK', 6, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-ski-008', 'prod-ski-008', 144, 0, 'WH-OSAKA-01', 'IN_STOCK', 28, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-ski-009', 'prod-ski-009', 27, 0, 'WH-NAGANO-01', 'IN_STOCK', 5, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-ski-010', 'prod-ski-010', 113, 0, 'WH-TOKYO-01', 'IN_STOCK', 22, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-ski-011', 'prod-ski-011', 13, 0, 'WH-OSAKA-01', 'IN_STOCK', 5, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-ski-012', 'prod-ski-012', 12, 0, 'WH-NAGANO-01', 'IN_STOCK', 5, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-ski-013', 'prod-ski-013', 28, 0, 'WH-TOKYO-01', 'IN_STOCK', 5, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-ski-014', 'prod-ski-014', 60, 0, 'WH-OSAKA-01', 'IN_STOCK', 12, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-ski-015', 'prod-ski-015', 64, 0, 'WH-NAGANO-01', 'IN_STOCK', 12, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-boot-001', 'prod-boot-001', 134, 0, 'WH-TOKYO-01', 'IN_STOCK', 26, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-boot-002', 'prod-boot-002', 11, 0, 'WH-OSAKA-01', 'IN_STOCK', 5, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-boot-003', 'prod-boot-003', 148, 0, 'WH-NAGANO-01', 'IN_STOCK', 29, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-boot-004', 'prod-boot-004', 55, 0, 'WH-TOKYO-01', 'IN_STOCK', 11, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-boot-005', 'prod-boot-005', 144, 0, 'WH-OSAKA-01', 'IN_STOCK', 28, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-boot-006', 'prod-boot-006', 112, 0, 'WH-NAGANO-01', 'IN_STOCK', 22, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-boot-007', 'prod-boot-007', 61, 0, 'WH-TOKYO-01', 'IN_STOCK', 12, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-boot-008', 'prod-boot-008', 119, 0, 'WH-OSAKA-01', 'IN_STOCK', 23, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-boot-009', 'prod-boot-009', 76, 0, 'WH-NAGANO-01', 'IN_STOCK', 15, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-boot-010', 'prod-boot-010', 6, 0, 'WH-TOKYO-01', 'LOW_STOCK', 5, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-boot-011', 'prod-boot-011', 45, 0, 'WH-OSAKA-01', 'IN_STOCK', 9, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-boot-012', 'prod-boot-012', 113, 0, 'WH-NAGANO-01', 'IN_STOCK', 22, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-boot-013', 'prod-boot-013', 92, 0, 'WH-TOKYO-01', 'IN_STOCK', 18, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-boot-014', 'prod-boot-014', 76, 0, 'WH-OSAKA-01', 'IN_STOCK', 15, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-boot-015', 'prod-boot-015', 44, 0, 'WH-NAGANO-01', 'IN_STOCK', 8, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-bind-001', 'prod-bind-001', 60, 0, 'WH-TOKYO-01', 'IN_STOCK', 12, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-bind-002', 'prod-bind-002', 91, 0, 'WH-OSAKA-01', 'IN_STOCK', 18, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-bind-003', 'prod-bind-003', 31, 0, 'WH-NAGANO-01', 'IN_STOCK', 6, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-bind-004', 'prod-bind-004', 28, 0, 'WH-TOKYO-01', 'IN_STOCK', 5, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-bind-005', 'prod-bind-005', 102, 0, 'WH-OSAKA-01', 'IN_STOCK', 20, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-bind-006', 'prod-bind-006', 29, 0, 'WH-NAGANO-01', 'IN_STOCK', 5, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-bind-007', 'prod-bind-007', 96, 0, 'WH-TOKYO-01', 'IN_STOCK', 19, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-bind-008', 'prod-bind-008', 93, 0, 'WH-OSAKA-01', 'IN_STOCK', 18, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-bind-009', 'prod-bind-009', 72, 0, 'WH-NAGANO-01', 'IN_STOCK', 14, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-bind-010', 'prod-bind-010', 16, 0, 'WH-TOKYO-01', 'IN_STOCK', 5, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-pole-001', 'prod-pole-001', 122, 0, 'WH-OSAKA-01', 'IN_STOCK', 24, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-pole-002', 'prod-pole-002', 142, 0, 'WH-NAGANO-01', 'IN_STOCK', 28, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-pole-003', 'prod-pole-003', 36, 0, 'WH-TOKYO-01', 'IN_STOCK', 7, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-pole-004', 'prod-pole-004', 101, 0, 'WH-OSAKA-01', 'IN_STOCK', 20, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-pole-005', 'prod-pole-005', 25, 0, 'WH-NAGANO-01', 'IN_STOCK', 5, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-gog-001', 'prod-gog-001', 146, 0, 'WH-TOKYO-01', 'IN_STOCK', 29, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-gog-002', 'prod-gog-002', 80, 0, 'WH-OSAKA-01', 'IN_STOCK', 16, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-gog-003', 'prod-gog-003', 97, 0, 'WH-NAGANO-01', 'IN_STOCK', 19, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-gog-004', 'prod-gog-004', 54, 0, 'WH-TOKYO-01', 'IN_STOCK', 10, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-gog-005', 'prod-gog-005', 22, 0, 'WH-OSAKA-01', 'IN_STOCK', 5, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-gog-006', 'prod-gog-006', 16, 0, 'WH-NAGANO-01', 'IN_STOCK', 5, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-gog-007', 'prod-gog-007', 63, 0, 'WH-TOKYO-01', 'IN_STOCK', 12, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-gog-008', 'prod-gog-008', 79, 0, 'WH-OSAKA-01', 'IN_STOCK', 15, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-gog-009', 'prod-gog-009', 25, 0, 'WH-NAGANO-01', 'IN_STOCK', 5, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-gog-010', 'prod-gog-010', 64, 0, 'WH-TOKYO-01', 'IN_STOCK', 12, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-helm-001', 'prod-helm-001', 30, 0, 'WH-OSAKA-01', 'IN_STOCK', 6, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-helm-002', 'prod-helm-002', 102, 0, 'WH-NAGANO-01', 'IN_STOCK', 20, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-helm-003', 'prod-helm-003', 76, 0, 'WH-TOKYO-01', 'IN_STOCK', 15, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-helm-004', 'prod-helm-004', 121, 0, 'WH-OSAKA-01', 'IN_STOCK', 24, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-helm-005', 'prod-helm-005', 98, 0, 'WH-NAGANO-01', 'IN_STOCK', 19, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-helm-006', 'prod-helm-006', 46, 0, 'WH-TOKYO-01', 'IN_STOCK', 9, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-helm-007', 'prod-helm-007', 99, 0, 'WH-OSAKA-01', 'IN_STOCK', 19, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-helm-008', 'prod-helm-008', 95, 0, 'WH-NAGANO-01', 'IN_STOCK', 19, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-helm-009', 'prod-helm-009', 58, 0, 'WH-TOKYO-01', 'IN_STOCK', 11, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-helm-010', 'prod-helm-010', 73, 0, 'WH-OSAKA-01', 'IN_STOCK', 14, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-glv-001', 'prod-glv-001', 23, 0, 'WH-NAGANO-01', 'IN_STOCK', 5, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-glv-002', 'prod-glv-002', 48, 0, 'WH-TOKYO-01', 'IN_STOCK', 9, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-glv-003', 'prod-glv-003', 141, 0, 'WH-OSAKA-01', 'IN_STOCK', 28, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-glv-004', 'prod-glv-004', 67, 0, 'WH-NAGANO-01', 'IN_STOCK', 13, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-glv-005', 'prod-glv-005', 46, 0, 'WH-TOKYO-01', 'IN_STOCK', 9, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-glv-006', 'prod-glv-006', 123, 0, 'WH-OSAKA-01', 'IN_STOCK', 24, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-glv-007', 'prod-glv-007', 102, 0, 'WH-NAGANO-01', 'IN_STOCK', 20, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-glv-008', 'prod-glv-008', 74, 0, 'WH-TOKYO-01', 'IN_STOCK', 14, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-glv-009', 'prod-glv-009', 147, 0, 'WH-OSAKA-01', 'IN_STOCK', 29, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-glv-010', 'prod-glv-010', 61, 0, 'WH-NAGANO-01', 'IN_STOCK', 12, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-jkt-001', 'prod-jkt-001', 88, 0, 'WH-TOKYO-01', 'IN_STOCK', 17, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-jkt-002', 'prod-jkt-002', 19, 0, 'WH-OSAKA-01', 'IN_STOCK', 5, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-jkt-003', 'prod-jkt-003', 63, 0, 'WH-NAGANO-01', 'IN_STOCK', 12, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-jkt-004', 'prod-jkt-004', 13, 0, 'WH-TOKYO-01', 'IN_STOCK', 5, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-jkt-005', 'prod-jkt-005', 85, 0, 'WH-OSAKA-01', 'IN_STOCK', 17, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-jkt-006', 'prod-jkt-006', 107, 0, 'WH-NAGANO-01', 'IN_STOCK', 21, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-jkt-007', 'prod-jkt-007', 73, 0, 'WH-TOKYO-01', 'IN_STOCK', 14, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-jkt-008', 'prod-jkt-008', 21, 0, 'WH-OSAKA-01', 'IN_STOCK', 5, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-jkt-009', 'prod-jkt-009', 59, 0, 'WH-NAGANO-01', 'IN_STOCK', 11, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-jkt-010', 'prod-jkt-010', 150, 0, 'WH-TOKYO-01', 'IN_STOCK', 30, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-pnt-001', 'prod-pnt-001', 85, 0, 'WH-OSAKA-01', 'IN_STOCK', 17, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-pnt-002', 'prod-pnt-002', 59, 0, 'WH-NAGANO-01', 'IN_STOCK', 11, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-pnt-003', 'prod-pnt-003', 132, 0, 'WH-TOKYO-01', 'IN_STOCK', 26, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-pnt-004', 'prod-pnt-004', 106, 0, 'WH-OSAKA-01', 'IN_STOCK', 21, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-pnt-005', 'prod-pnt-005', 122, 0, 'WH-NAGANO-01', 'IN_STOCK', 24, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-pnt-006', 'prod-pnt-006', 41, 0, 'WH-TOKYO-01', 'IN_STOCK', 8, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-pnt-007', 'prod-pnt-007', 72, 0, 'WH-OSAKA-01', 'IN_STOCK', 14, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-pnt-008', 'prod-pnt-008', 40, 0, 'WH-NAGANO-01', 'IN_STOCK', 8, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-pnt-009', 'prod-pnt-009', 68, 0, 'WH-TOKYO-01', 'IN_STOCK', 13, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-pnt-010', 'prod-pnt-010', 148, 0, 'WH-OSAKA-01', 'IN_STOCK', 29, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-acc-001', 'prod-acc-001', 142, 0, 'WH-NAGANO-01', 'IN_STOCK', 28, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-acc-002', 'prod-acc-002', 72, 0, 'WH-TOKYO-01', 'IN_STOCK', 14, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-acc-003', 'prod-acc-003', 114, 0, 'WH-OSAKA-01', 'IN_STOCK', 22, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-acc-004', 'prod-acc-004', 107, 0, 'WH-NAGANO-01', 'IN_STOCK', 21, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO inventories (id, product_id, quantity, reserved_quantity, location_code, status, reorder_point, reserved_at, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('inv-acc-005', 'prod-acc-005', 97, 0, 'WH-TOKYO-01', 'IN_STOCK', 19, NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

-- ---------------------------------------------------------------------------
-- 4. product_images テーブル（各商品に 1 メイン画像 = 100 件）
-- ---------------------------------------------------------------------------

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-ski-001', 'prod-ski-001', 'https://images.skishop.example.com/products/prod-ski-001/main.webp', 'https://images.skishop.example.com/products/prod-ski-001/thumb.webp', 'MAIN', 1, 'Salomon Salomon S/Force Bold', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-ski-002', 'prod-ski-002', 'https://images.skishop.example.com/products/prod-ski-002/main.webp', 'https://images.skishop.example.com/products/prod-ski-002/thumb.webp', 'MAIN', 1, 'Atomic Atomic Redster X9S', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-ski-003', 'prod-ski-003', 'https://images.skishop.example.com/products/prod-ski-003/main.webp', 'https://images.skishop.example.com/products/prod-ski-003/thumb.webp', 'MAIN', 1, 'HEAD HEAD Supershape e-Speed', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-ski-004', 'prod-ski-004', 'https://images.skishop.example.com/products/prod-ski-004/main.webp', 'https://images.skishop.example.com/products/prod-ski-004/thumb.webp', 'MAIN', 1, 'Rossignol Rossignol Experience 86 Ti', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-ski-005', 'prod-ski-005', 'https://images.skishop.example.com/products/prod-ski-005/main.webp', 'https://images.skishop.example.com/products/prod-ski-005/thumb.webp', 'MAIN', 1, 'Volkl Volkl Deacon 76', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-ski-006', 'prod-ski-006', 'https://images.skishop.example.com/products/prod-ski-006/main.webp', 'https://images.skishop.example.com/products/prod-ski-006/thumb.webp', 'MAIN', 1, 'K2 K2 Mindbender 99Ti', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-ski-007', 'prod-ski-007', 'https://images.skishop.example.com/products/prod-ski-007/main.webp', 'https://images.skishop.example.com/products/prod-ski-007/thumb.webp', 'MAIN', 1, 'Blizzard Blizzard Brahma 88', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-ski-008', 'prod-ski-008', 'https://images.skishop.example.com/products/prod-ski-008/main.webp', 'https://images.skishop.example.com/products/prod-ski-008/thumb.webp', 'MAIN', 1, 'Fischer Fischer RC4 The Curv DTX', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-ski-009', 'prod-ski-009', 'https://images.skishop.example.com/products/prod-ski-009/main.webp', 'https://images.skishop.example.com/products/prod-ski-009/thumb.webp', 'MAIN', 1, 'Nordica Nordica Enforcer 100', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-ski-010', 'prod-ski-010', 'https://images.skishop.example.com/products/prod-ski-010/main.webp', 'https://images.skishop.example.com/products/prod-ski-010/thumb.webp', 'MAIN', 1, 'Elan Elan Wingman 86 CTi', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-ski-011', 'prod-ski-011', 'https://images.skishop.example.com/products/prod-ski-011/main.webp', 'https://images.skishop.example.com/products/prod-ski-011/thumb.webp', 'MAIN', 1, 'Salomon Salomon QST 106', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-ski-012', 'prod-ski-012', 'https://images.skishop.example.com/products/prod-ski-012/main.webp', 'https://images.skishop.example.com/products/prod-ski-012/thumb.webp', 'MAIN', 1, 'Atomic Atomic Vantage 79 Ti', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-ski-013', 'prod-ski-013', 'https://images.skishop.example.com/products/prod-ski-013/main.webp', 'https://images.skishop.example.com/products/prod-ski-013/thumb.webp', 'MAIN', 1, 'HEAD HEAD WC Rebels e-GS RD', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-ski-014', 'prod-ski-014', 'https://images.skishop.example.com/products/prod-ski-014/main.webp', 'https://images.skishop.example.com/products/prod-ski-014/thumb.webp', 'MAIN', 1, 'Rossignol Rossignol Hero Elite ST Ti', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-ski-015', 'prod-ski-015', 'https://images.skishop.example.com/products/prod-ski-015/main.webp', 'https://images.skishop.example.com/products/prod-ski-015/thumb.webp', 'MAIN', 1, 'Fischer Fischer Ranger 102 FR', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-boot-001', 'prod-boot-001', 'https://images.skishop.example.com/products/prod-boot-001/main.webp', 'https://images.skishop.example.com/products/prod-boot-001/thumb.webp', 'MAIN', 1, 'Salomon Salomon S/PRO Alpha 120', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-boot-002', 'prod-boot-002', 'https://images.skishop.example.com/products/prod-boot-002/main.webp', 'https://images.skishop.example.com/products/prod-boot-002/thumb.webp', 'MAIN', 1, 'Atomic Atomic Hawx Ultra 130', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-boot-003', 'prod-boot-003', 'https://images.skishop.example.com/products/prod-boot-003/main.webp', 'https://images.skishop.example.com/products/prod-boot-003/thumb.webp', 'MAIN', 1, 'Tecnica Tecnica Mach1 MV 120', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-boot-004', 'prod-boot-004', 'https://images.skishop.example.com/products/prod-boot-004/main.webp', 'https://images.skishop.example.com/products/prod-boot-004/thumb.webp', 'MAIN', 1, 'Lange Lange RX 130', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-boot-005', 'prod-boot-005', 'https://images.skishop.example.com/products/prod-boot-005/main.webp', 'https://images.skishop.example.com/products/prod-boot-005/thumb.webp', 'MAIN', 1, 'HEAD HEAD Raptor WCR 140', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-boot-006', 'prod-boot-006', 'https://images.skishop.example.com/products/prod-boot-006/main.webp', 'https://images.skishop.example.com/products/prod-boot-006/thumb.webp', 'MAIN', 1, 'Rossignol Rossignol Speed 100', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-boot-007', 'prod-boot-007', 'https://images.skishop.example.com/products/prod-boot-007/main.webp', 'https://images.skishop.example.com/products/prod-boot-007/thumb.webp', 'MAIN', 1, 'Nordica Nordica Speedmachine 3 130', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-boot-008', 'prod-boot-008', 'https://images.skishop.example.com/products/prod-boot-008/main.webp', 'https://images.skishop.example.com/products/prod-boot-008/thumb.webp', 'MAIN', 1, 'Fischer Fischer RC4 The Curv 130', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-boot-009', 'prod-boot-009', 'https://images.skishop.example.com/products/prod-boot-009/main.webp', 'https://images.skishop.example.com/products/prod-boot-009/thumb.webp', 'MAIN', 1, 'Dalbello Dalbello Lupo AX 120', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-boot-010', 'prod-boot-010', 'https://images.skishop.example.com/products/prod-boot-010/main.webp', 'https://images.skishop.example.com/products/prod-boot-010/thumb.webp', 'MAIN', 1, 'Salomon Salomon S/PRO Supra BOA 110', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-boot-011', 'prod-boot-011', 'https://images.skishop.example.com/products/prod-boot-011/main.webp', 'https://images.skishop.example.com/products/prod-boot-011/thumb.webp', 'MAIN', 1, 'Atomic Atomic Hawx Prime 100', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-boot-012', 'prod-boot-012', 'https://images.skishop.example.com/products/prod-boot-012/main.webp', 'https://images.skishop.example.com/products/prod-boot-012/thumb.webp', 'MAIN', 1, 'HEAD HEAD Edge LYT 100', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-boot-013', 'prod-boot-013', 'https://images.skishop.example.com/products/prod-boot-013/main.webp', 'https://images.skishop.example.com/products/prod-boot-013/thumb.webp', 'MAIN', 1, 'K2 K2 Recon 120 MV', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-boot-014', 'prod-boot-014', 'https://images.skishop.example.com/products/prod-boot-014/main.webp', 'https://images.skishop.example.com/products/prod-boot-014/thumb.webp', 'MAIN', 1, 'Scarpa Scarpa Maestrale RS', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-boot-015', 'prod-boot-015', 'https://images.skishop.example.com/products/prod-boot-015/main.webp', 'https://images.skishop.example.com/products/prod-boot-015/thumb.webp', 'MAIN', 1, 'Tecnica Tecnica Cochise 120 DYN', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-bind-001', 'prod-bind-001', 'https://images.skishop.example.com/products/prod-bind-001/main.webp', 'https://images.skishop.example.com/products/prod-bind-001/thumb.webp', 'MAIN', 1, 'Marker Marker Griffon 13 ID', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-bind-002', 'prod-bind-002', 'https://images.skishop.example.com/products/prod-bind-002/main.webp', 'https://images.skishop.example.com/products/prod-bind-002/thumb.webp', 'MAIN', 1, 'Tyrolia Tyrolia Attack2 14 GW', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-bind-003', 'prod-bind-003', 'https://images.skishop.example.com/products/prod-bind-003/main.webp', 'https://images.skishop.example.com/products/prod-bind-003/thumb.webp', 'MAIN', 1, 'Look Look Pivot 15 GW', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-bind-004', 'prod-bind-004', 'https://images.skishop.example.com/products/prod-bind-004/main.webp', 'https://images.skishop.example.com/products/prod-bind-004/thumb.webp', 'MAIN', 1, 'Salomon Salomon STH2 WTR 13', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-bind-005', 'prod-bind-005', 'https://images.skishop.example.com/products/prod-bind-005/main.webp', 'https://images.skishop.example.com/products/prod-bind-005/thumb.webp', 'MAIN', 1, 'Atomic Atomic Warden MNC 13', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-bind-006', 'prod-bind-006', 'https://images.skishop.example.com/products/prod-bind-006/main.webp', 'https://images.skishop.example.com/products/prod-bind-006/thumb.webp', 'MAIN', 1, 'Marker Marker Kingpin 13', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-bind-007', 'prod-bind-007', 'https://images.skishop.example.com/products/prod-bind-007/main.webp', 'https://images.skishop.example.com/products/prod-bind-007/thumb.webp', 'MAIN', 1, 'Tyrolia Tyrolia AAAttack2 11 GW', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-bind-008', 'prod-bind-008', 'https://images.skishop.example.com/products/prod-bind-008/main.webp', 'https://images.skishop.example.com/products/prod-bind-008/thumb.webp', 'MAIN', 1, 'Look Look SPX 12 GW', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-bind-009', 'prod-bind-009', 'https://images.skishop.example.com/products/prod-bind-009/main.webp', 'https://images.skishop.example.com/products/prod-bind-009/thumb.webp', 'MAIN', 1, 'Salomon Salomon Shift MNC 13', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-bind-010', 'prod-bind-010', 'https://images.skishop.example.com/products/prod-bind-010/main.webp', 'https://images.skishop.example.com/products/prod-bind-010/thumb.webp', 'MAIN', 1, 'HEAD HEAD Freeflex ST 14', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-pole-001', 'prod-pole-001', 'https://images.skishop.example.com/products/prod-pole-001/main.webp', 'https://images.skishop.example.com/products/prod-pole-001/thumb.webp', 'MAIN', 1, 'LEKI LEKI WCR GS', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-pole-002', 'prod-pole-002', 'https://images.skishop.example.com/products/prod-pole-002/main.webp', 'https://images.skishop.example.com/products/prod-pole-002/thumb.webp', 'MAIN', 1, 'Black Diamond Black Diamond Traverse', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-pole-003', 'prod-pole-003', 'https://images.skishop.example.com/products/prod-pole-003/main.webp', 'https://images.skishop.example.com/products/prod-pole-003/thumb.webp', 'MAIN', 1, 'Salomon Salomon Arctic', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-pole-004', 'prod-pole-004', 'https://images.skishop.example.com/products/prod-pole-004/main.webp', 'https://images.skishop.example.com/products/prod-pole-004/thumb.webp', 'MAIN', 1, 'Atomic Atomic Redster Carbon', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-pole-005', 'prod-pole-005', 'https://images.skishop.example.com/products/prod-pole-005/main.webp', 'https://images.skishop.example.com/products/prod-pole-005/thumb.webp', 'MAIN', 1, 'Komperdell Komperdell Nationalteam Carbon', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-gog-001', 'prod-gog-001', 'https://images.skishop.example.com/products/prod-gog-001/main.webp', 'https://images.skishop.example.com/products/prod-gog-001/thumb.webp', 'MAIN', 1, 'Oakley Oakley Flight Deck L', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-gog-002', 'prod-gog-002', 'https://images.skishop.example.com/products/prod-gog-002/main.webp', 'https://images.skishop.example.com/products/prod-gog-002/thumb.webp', 'MAIN', 1, 'Smith Smith I/O Mag', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-gog-003', 'prod-gog-003', 'https://images.skishop.example.com/products/prod-gog-003/main.webp', 'https://images.skishop.example.com/products/prod-gog-003/thumb.webp', 'MAIN', 1, 'POC POC Orb Clarity', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-gog-004', 'prod-gog-004', 'https://images.skishop.example.com/products/prod-gog-004/main.webp', 'https://images.skishop.example.com/products/prod-gog-004/thumb.webp', 'MAIN', 1, 'UVEX UVEX Downhill 2100 CV', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-gog-005', 'prod-gog-005', 'https://images.skishop.example.com/products/prod-gog-005/main.webp', 'https://images.skishop.example.com/products/prod-gog-005/thumb.webp', 'MAIN', 1, 'Giro Giro Contour RS', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-gog-006', 'prod-gog-006', 'https://images.skishop.example.com/products/prod-gog-006/main.webp', 'https://images.skishop.example.com/products/prod-gog-006/thumb.webp', 'MAIN', 1, 'Oakley Oakley Line Miner L', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-gog-007', 'prod-gog-007', 'https://images.skishop.example.com/products/prod-gog-007/main.webp', 'https://images.skishop.example.com/products/prod-gog-007/thumb.webp', 'MAIN', 1, 'Smith Smith Squad MAG', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-gog-008', 'prod-gog-008', 'https://images.skishop.example.com/products/prod-gog-008/main.webp', 'https://images.skishop.example.com/products/prod-gog-008/thumb.webp', 'MAIN', 1, 'Bolle Bolle Nevada', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-gog-009', 'prod-gog-009', 'https://images.skishop.example.com/products/prod-gog-009/main.webp', 'https://images.skishop.example.com/products/prod-gog-009/thumb.webp', 'MAIN', 1, 'SWANS SWANS ROVO-MDH-CU/LI', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-gog-010', 'prod-gog-010', 'https://images.skishop.example.com/products/prod-gog-010/main.webp', 'https://images.skishop.example.com/products/prod-gog-010/thumb.webp', 'MAIN', 1, 'Dragon Dragon X2s', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-helm-001', 'prod-helm-001', 'https://images.skishop.example.com/products/prod-helm-001/main.webp', 'https://images.skishop.example.com/products/prod-helm-001/thumb.webp', 'MAIN', 1, 'Giro Giro Range MIPS', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-helm-002', 'prod-helm-002', 'https://images.skishop.example.com/products/prod-helm-002/main.webp', 'https://images.skishop.example.com/products/prod-helm-002/thumb.webp', 'MAIN', 1, 'Smith Smith Vantage MIPS', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-helm-003', 'prod-helm-003', 'https://images.skishop.example.com/products/prod-helm-003/main.webp', 'https://images.skishop.example.com/products/prod-helm-003/thumb.webp', 'MAIN', 1, 'POC POC Obex MIPS', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-helm-004', 'prod-helm-004', 'https://images.skishop.example.com/products/prod-helm-004/main.webp', 'https://images.skishop.example.com/products/prod-helm-004/thumb.webp', 'MAIN', 1, 'Oakley Oakley MOD5 MIPS', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-helm-005', 'prod-helm-005', 'https://images.skishop.example.com/products/prod-helm-005/main.webp', 'https://images.skishop.example.com/products/prod-helm-005/thumb.webp', 'MAIN', 1, 'UVEX UVEX Legend 2.0', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-helm-006', 'prod-helm-006', 'https://images.skishop.example.com/products/prod-helm-006/main.webp', 'https://images.skishop.example.com/products/prod-helm-006/thumb.webp', 'MAIN', 1, 'Sweet Protection Sweet Protection Switcher MIPS', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-helm-007', 'prod-helm-007', 'https://images.skishop.example.com/products/prod-helm-007/main.webp', 'https://images.skishop.example.com/products/prod-helm-007/thumb.webp', 'MAIN', 1, 'Salomon Salomon MTN Lab', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-helm-008', 'prod-helm-008', 'https://images.skishop.example.com/products/prod-helm-008/main.webp', 'https://images.skishop.example.com/products/prod-helm-008/thumb.webp', 'MAIN', 1, 'Atomic Atomic Savor AMID', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-helm-009', 'prod-helm-009', 'https://images.skishop.example.com/products/prod-helm-009/main.webp', 'https://images.skishop.example.com/products/prod-helm-009/thumb.webp', 'MAIN', 1, 'HEAD HEAD Radar MIPS', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-helm-010', 'prod-helm-010', 'https://images.skishop.example.com/products/prod-helm-010/main.webp', 'https://images.skishop.example.com/products/prod-helm-010/thumb.webp', 'MAIN', 1, 'K2 K2 Verdict MIPS', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-glv-001', 'prod-glv-001', 'https://images.skishop.example.com/products/prod-glv-001/main.webp', 'https://images.skishop.example.com/products/prod-glv-001/thumb.webp', 'MAIN', 1, 'Hestra Hestra Army Leather Heli Ski', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-glv-002', 'prod-glv-002', 'https://images.skishop.example.com/products/prod-glv-002/main.webp', 'https://images.skishop.example.com/products/prod-glv-002/thumb.webp', 'MAIN', 1, 'Black Diamond Black Diamond Guide Glove', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-glv-003', 'prod-glv-003', 'https://images.skishop.example.com/products/prod-glv-003/main.webp', 'https://images.skishop.example.com/products/prod-glv-003/thumb.webp', 'MAIN', 1, 'Outdoor Research Outdoor Research Stormtracker', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-glv-004', 'prod-glv-004', 'https://images.skishop.example.com/products/prod-glv-004/main.webp', 'https://images.skishop.example.com/products/prod-glv-004/thumb.webp', 'MAIN', 1, 'Hestra Hestra Fall Line', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-glv-005', 'prod-glv-005', 'https://images.skishop.example.com/products/prod-glv-005/main.webp', 'https://images.skishop.example.com/products/prod-glv-005/thumb.webp', 'MAIN', 1, 'Dakine Dakine Titan GORE-TEX', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-glv-006', 'prod-glv-006', 'https://images.skishop.example.com/products/prod-glv-006/main.webp', 'https://images.skishop.example.com/products/prod-glv-006/thumb.webp', 'MAIN', 1, 'LEKI LEKI Griffin S', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-glv-007', 'prod-glv-007', 'https://images.skishop.example.com/products/prod-glv-007/main.webp', 'https://images.skishop.example.com/products/prod-glv-007/thumb.webp', 'MAIN', 1, 'Reusch Reusch Pro RC', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-glv-008', 'prod-glv-008', 'https://images.skishop.example.com/products/prod-glv-008/main.webp', 'https://images.skishop.example.com/products/prod-glv-008/thumb.webp', 'MAIN', 1, 'POC POC Palm Comp VPD 2.0', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-glv-009', 'prod-glv-009', 'https://images.skishop.example.com/products/prod-glv-009/main.webp', 'https://images.skishop.example.com/products/prod-glv-009/thumb.webp', 'MAIN', 1, 'Salomon Salomon Force GORE-TEX', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-glv-010', 'prod-glv-010', 'https://images.skishop.example.com/products/prod-glv-010/main.webp', 'https://images.skishop.example.com/products/prod-glv-010/thumb.webp', 'MAIN', 1, 'Therm-ic Therm-ic PowerGloves 3+1', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-jkt-001', 'prod-jkt-001', 'https://images.skishop.example.com/products/prod-jkt-001/main.webp', 'https://images.skishop.example.com/products/prod-jkt-001/thumb.webp', 'MAIN', 1, 'Arcteryx Arcteryx Rush Jacket', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-jkt-002', 'prod-jkt-002', 'https://images.skishop.example.com/products/prod-jkt-002/main.webp', 'https://images.skishop.example.com/products/prod-jkt-002/thumb.webp', 'MAIN', 1, 'Patagonia Patagonia PowSlayer Jacket', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-jkt-003', 'prod-jkt-003', 'https://images.skishop.example.com/products/prod-jkt-003/main.webp', 'https://images.skishop.example.com/products/prod-jkt-003/thumb.webp', 'MAIN', 1, 'The North Face The North Face Ceptor Jacket', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-jkt-004', 'prod-jkt-004', 'https://images.skishop.example.com/products/prod-jkt-004/main.webp', 'https://images.skishop.example.com/products/prod-jkt-004/thumb.webp', 'MAIN', 1, 'Salomon Salomon Brilliant Jacket', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-jkt-005', 'prod-jkt-005', 'https://images.skishop.example.com/products/prod-jkt-005/main.webp', 'https://images.skishop.example.com/products/prod-jkt-005/thumb.webp', 'MAIN', 1, 'Mammut Mammut Stoney HS Jacket', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-jkt-006', 'prod-jkt-006', 'https://images.skishop.example.com/products/prod-jkt-006/main.webp', 'https://images.skishop.example.com/products/prod-jkt-006/thumb.webp', 'MAIN', 1, 'Helly Hansen Helly Hansen Alpha 4.0 Jacket', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-jkt-007', 'prod-jkt-007', 'https://images.skishop.example.com/products/prod-jkt-007/main.webp', 'https://images.skishop.example.com/products/prod-jkt-007/thumb.webp', 'MAIN', 1, 'Norrona Norrona Lofoten GORE-TEX Pro Jacket', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-jkt-008', 'prod-jkt-008', 'https://images.skishop.example.com/products/prod-jkt-008/main.webp', 'https://images.skishop.example.com/products/prod-jkt-008/thumb.webp', 'MAIN', 1, 'Descente Descente Swiss Ski Team Jacket', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-jkt-009', 'prod-jkt-009', 'https://images.skishop.example.com/products/prod-jkt-009/main.webp', 'https://images.skishop.example.com/products/prod-jkt-009/thumb.webp', 'MAIN', 1, 'Phenix Phenix Twinpeaks Jacket', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-jkt-010', 'prod-jkt-010', 'https://images.skishop.example.com/products/prod-jkt-010/main.webp', 'https://images.skishop.example.com/products/prod-jkt-010/thumb.webp', 'MAIN', 1, 'Goldwin Goldwin G-Bliss Jacket', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-pnt-001', 'prod-pnt-001', 'https://images.skishop.example.com/products/prod-pnt-001/main.webp', 'https://images.skishop.example.com/products/prod-pnt-001/thumb.webp', 'MAIN', 1, 'Arcteryx Arcteryx Sabre AR Pant', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-pnt-002', 'prod-pnt-002', 'https://images.skishop.example.com/products/prod-pnt-002/main.webp', 'https://images.skishop.example.com/products/prod-pnt-002/thumb.webp', 'MAIN', 1, 'Salomon Salomon Stance 3L Pant', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-pnt-003', 'prod-pnt-003', 'https://images.skishop.example.com/products/prod-pnt-003/main.webp', 'https://images.skishop.example.com/products/prod-pnt-003/thumb.webp', 'MAIN', 1, 'The North Face The North Face Freedom Insulated Pant', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-pnt-004', 'prod-pnt-004', 'https://images.skishop.example.com/products/prod-pnt-004/main.webp', 'https://images.skishop.example.com/products/prod-pnt-004/thumb.webp', 'MAIN', 1, 'Patagonia Patagonia Insulated Powder Town Pants', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-pnt-005', 'prod-pnt-005', 'https://images.skishop.example.com/products/prod-pnt-005/main.webp', 'https://images.skishop.example.com/products/prod-pnt-005/thumb.webp', 'MAIN', 1, 'Helly Hansen Helly Hansen Legendary Insulated Pant', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-pnt-006', 'prod-pnt-006', 'https://images.skishop.example.com/products/prod-pnt-006/main.webp', 'https://images.skishop.example.com/products/prod-pnt-006/thumb.webp', 'MAIN', 1, 'Mammut Mammut Haldigrat HS Pants', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-pnt-007', 'prod-pnt-007', 'https://images.skishop.example.com/products/prod-pnt-007/main.webp', 'https://images.skishop.example.com/products/prod-pnt-007/thumb.webp', 'MAIN', 1, 'Descente Descente Swiss Team Pants', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-pnt-008', 'prod-pnt-008', 'https://images.skishop.example.com/products/prod-pnt-008/main.webp', 'https://images.skishop.example.com/products/prod-pnt-008/thumb.webp', 'MAIN', 1, 'Phenix Phenix Blizzard Pants', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-pnt-009', 'prod-pnt-009', 'https://images.skishop.example.com/products/prod-pnt-009/main.webp', 'https://images.skishop.example.com/products/prod-pnt-009/thumb.webp', 'MAIN', 1, 'Goldwin Goldwin G-Bliss Pants', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-pnt-010', 'prod-pnt-010', 'https://images.skishop.example.com/products/prod-pnt-010/main.webp', 'https://images.skishop.example.com/products/prod-pnt-010/thumb.webp', 'MAIN', 1, 'Norrona Norrona Lofoten GORE-TEX Pro Pants', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-acc-001', 'prod-acc-001', 'https://images.skishop.example.com/products/prod-acc-001/main.webp', 'https://images.skishop.example.com/products/prod-acc-001/thumb.webp', 'MAIN', 1, 'TOKO TOKO All-in-One Hot Wax', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-acc-002', 'prod-acc-002', 'https://images.skishop.example.com/products/prod-acc-002/main.webp', 'https://images.skishop.example.com/products/prod-acc-002/thumb.webp', 'MAIN', 1, 'GALLIUM GALLIUM Extra Base Violet', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-acc-003', 'prod-acc-003', 'https://images.skishop.example.com/products/prod-acc-003/main.webp', 'https://images.skishop.example.com/products/prod-acc-003/thumb.webp', 'MAIN', 1, 'Deuter Deuter Freerider 30', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-acc-004', 'prod-acc-004', 'https://images.skishop.example.com/products/prod-acc-004/main.webp', 'https://images.skishop.example.com/products/prod-acc-004/thumb.webp', 'MAIN', 1, 'Buff Buff Thermonet Neckwarmer', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

INSERT INTO product_images (id, product_id, url, thumbnail_url, type, sort_order, alt_text, created_at, updated_at, created_by, updated_by, row_version)
VALUES ('img-acc-005', 'prod-acc-005', 'https://images.skishop.example.com/products/prod-acc-005/main.webp', 'https://images.skishop.example.com/products/prod-acc-005/thumb.webp', 'MAIN', 1, 'Swix Swix T76-2 Waxing Iron', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'seed', 'seed', E'\\x'::bytea)
ON CONFLICT (id) DO NOTHING;

COMMIT;

-- 投入件数サマリー:
-- products:       100 件
-- prices:         100 件
-- inventories:    100 件
-- product_images: 100 件
-- 合計:           400 件
