-- =============================================================================
-- SkiShop InventoryManagementService — カテゴリシードデータ
-- Database: inventorydb (PostgreSQL)
-- 
-- 概要:
--   スキー用品 EC サイトの商品カテゴリマスタデータ（11 件）を投入する。
--   ルートカテゴリ（level 0）1 件と、その直下のサブカテゴリ（level 1）10 件で
--   構成される 2 階層の階層構造を持つ。
--
-- べき等性:
--   INSERT ... ON CONFLICT (id) DO NOTHING を使用し、再実行しても安全。
--
-- 実行順序: 02（01-schema の後に実行）
-- =============================================================================

-- ---------------------------------------------------------------------------
-- categories テーブルへのシードデータ投入
-- ---------------------------------------------------------------------------

INSERT INTO categories (id, name, description, parent_id, level, path, active, created_at, updated_at, row_version)
VALUES
    -- ルートカテゴリ (level 0)
    (
        'cat-root',
        'スキー用品',
        'スキー・スノースポーツに関する全商品のルートカテゴリ。スキー板、ブーツ、ウェア、アクセサリーなど幅広いカテゴリを包含します。',
        NULL,
        0,
        'cat-root',
        true,
        CURRENT_TIMESTAMP,
        CURRENT_TIMESTAMP,
        E'\\x'::bytea
    ),

    -- サブカテゴリ (level 1)
    (
        'cat-ski',
        'スキー板',
        'オールマウンテン、フリーライド、レーシング、基礎スキーなど各種スキー板を取り揃えています。初心者向けからエキスパート向けまで幅広いラインナップ。',
        'cat-root',
        1,
        'cat-root/cat-ski',
        true,
        CURRENT_TIMESTAMP,
        CURRENT_TIMESTAMP,
        E'\\x'::bytea
    ),
    (
        'cat-boots',
        'スキーブーツ',
        'レーシング、フリーライド、基礎スキー向けのスキーブーツ。フレックス、ラスト幅、歩行モードなど多様なニーズに対応するモデルを揃えています。',
        'cat-root',
        1,
        'cat-root/cat-boots',
        true,
        CURRENT_TIMESTAMP,
        CURRENT_TIMESTAMP,
        E'\\x'::bytea
    ),
    (
        'cat-bindings',
        'ビンディング',
        'アルペンビンディング、ツアービンディング、フリーライド向けなど各種ビンディング。DIN 値や重量、互換性で選べます。',
        'cat-root',
        1,
        'cat-root/cat-bindings',
        true,
        CURRENT_TIMESTAMP,
        CURRENT_TIMESTAMP,
        E'\\x'::bytea
    ),
    (
        'cat-poles',
        'ストック・ポール',
        'レーシング用カーボンポールからバックカントリー向け伸縮ポールまで。素材、グリップ形状、長さ調節機能で選べます。',
        'cat-root',
        1,
        'cat-root/cat-poles',
        true,
        CURRENT_TIMESTAMP,
        CURRENT_TIMESTAMP,
        E'\\x'::bytea
    ),
    (
        'cat-goggles',
        'ゴーグル',
        '各種レンズテクノロジー搭載のスキー・スノーボードゴーグル。視認性、フィット感、換気性能に優れたモデルを取り揃えています。',
        'cat-root',
        1,
        'cat-root/cat-goggles',
        true,
        CURRENT_TIMESTAMP,
        CURRENT_TIMESTAMP,
        E'\\x'::bytea
    ),
    (
        'cat-helmets',
        'ヘルメット',
        'MIPS テクノロジー搭載モデルを中心としたスキーヘルメット。安全性、通気性、フィット感に優れた各ブランドのモデルを揃えています。',
        'cat-root',
        1,
        'cat-root/cat-helmets',
        true,
        CURRENT_TIMESTAMP,
        CURRENT_TIMESTAMP,
        E'\\x'::bytea
    ),
    (
        'cat-gloves',
        'グローブ',
        'レザーグローブ、GORE-TEX 防水グローブ、ヒーター内蔵モデルなど。保温性、防水性、操作性に優れた各種スキーグローブ。',
        'cat-root',
        1,
        'cat-root/cat-gloves',
        true,
        CURRENT_TIMESTAMP,
        CURRENT_TIMESTAMP,
        E'\\x'::bytea
    ),
    (
        'cat-jackets',
        'スキージャケット',
        '高機能シェルジャケットからインサレーションジャケットまで。防水透湿性、保温性、運動性に優れたスキーウェアジャケット。',
        'cat-root',
        1,
        'cat-root/cat-jackets',
        true,
        CURRENT_TIMESTAMP,
        CURRENT_TIMESTAMP,
        E'\\x'::bytea
    ),
    (
        'cat-pants',
        'スキーパンツ',
        '防水透湿素材を使用したスキーパンツ。インサレーション入りからシェルタイプまで、動きやすさと防寒性を両立したモデル。',
        'cat-root',
        1,
        'cat-root/cat-pants',
        true,
        CURRENT_TIMESTAMP,
        CURRENT_TIMESTAMP,
        E'\\x'::bytea
    ),
    (
        'cat-accessories',
        'アクセサリー',
        'ネックウォーマー、バックパック、ワックスキットなどスキーに必要な各種アクセサリー。快適なスキーライフをサポートするアイテム。',
        'cat-root',
        1,
        'cat-root/cat-accessories',
        true,
        CURRENT_TIMESTAMP,
        CURRENT_TIMESTAMP,
        E'\\x'::bytea
    )
ON CONFLICT (id) DO NOTHING;
