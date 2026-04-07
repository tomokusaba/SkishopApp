-- ============================================================
-- SkiShop: PostgreSQL 初期化スクリプト
-- 各マイクロサービス用のデータベースを作成する。
-- docker-compose.yml の volumes マウントにより初回起動時に自動実行される。
-- ============================================================

CREATE DATABASE authdb;
CREATE DATABASE userdb;
CREATE DATABASE inventorydb;
CREATE DATABASE salesdb;
CREATE DATABASE cartdb;
CREATE DATABASE coupondb;
CREATE DATABASE pointdb;
CREATE DATABASE mailsenddb;
CREATE DATABASE aisupportdb;
