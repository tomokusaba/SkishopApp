# SkiShop サンプルデータ投入ガイド (INSERT_DATA.md)

## 1. 概要

本ドキュメントでは、SkiShop マイクロサービス群に対して**開発用サンプルデータ**を一括投入する方法を説明します。  
サンプルデータには、実在するスキー用品メーカーの商品情報（100 件）を含む、全マイクロサービスにわたるリアルなテストデータが含まれます。

---

## 2. 推奨アプローチ: SQL シードスクリプト方式

### なぜ SQL シードスクリプト方式か

| 方式 | メリット | デメリット | 採用 |
|------|---------|----------|------|
| **SQL シードスクリプト** | 高速・再現性◎・依存関係なし・バージョン管理可能 | EF Core マイグレーションと別管理 | ✅ **採用** |
| EF Core `HasData()` | マイグレーションと統合 | ビルド必要・変更のたびにマイグレーション再生成 | ❌ |
| C# アプリケーションシード | 型安全 | 全サービス起動が必要・遅い | ❌ |
| CSV インポート | Excel 編集可能 | 型変換・制約対応が煩雑 | ❌ |

### 設計方針

1. **べき等性**: すべての INSERT に `ON CONFLICT (id) DO NOTHING` を使用。何度実行しても安全
2. **依存順序**: データの FK 依存関係を考慮した投入順序を保証
3. **リセット可能**: `--reset` オプションで既存シードデータのみ削除して再投入
4. **決定的 ID**: ランダム UUID ではなく `prod-ski-001` のような予測可能な ID を使用

---

## 3. ファイル構成

```
infra/seed-data/
├── seed-all.sh                    # 一括投入スクリプト（メイン）
├── 01-auth-seed.sql               # 認証: テストユーザー 10 名
├── 02-inventory-categories.sql    # 在庫: 商品カテゴリ 11 件
├── 03-inventory-products.sql      # 在庫: 商品・価格・在庫 100 件
├── 04-inventory-reviews.sql       # 在庫: 商品レビュー 30 件
├── 05-coupon-seed.sql             # クーポン: クーポン 8 件・キャンペーン 2 件
├── 06-point-seed.sql              # ポイント: アカウント 10 件・取引履歴
├── 07-user-management-seed.sql    # ユーザー管理: プロファイル・住所・ランク
├── 08-mail-templates-seed.sql     # メール: テンプレート 6 件
├── 09-sales-orders-seed.sql       # 販売: 注文 10 件
└── 10-cart-seed.sql               # カート: カート 3 件
```

---

## 4. 実行方法

### 前提条件

```bash
# Docker コンテナが起動していること
docker compose up -d
```

### サンプルデータの投入

```bash
# 全サービスにサンプルデータを投入
./infra/seed-data/seed-all.sh
```

### リセットして再投入

```bash
# 既存のシードデータを削除して再投入
./infra/seed-data/seed-all.sh --reset
```

### 個別サービスへの投入

```bash
# 特定のサービスのみ投入する場合
docker exec -i skishop-postgres psql -U skishop -d inventorydb < infra/seed-data/02-inventory-categories.sql
docker exec -i skishop-postgres psql -U skishop -d inventorydb < infra/seed-data/03-inventory-products.sql
```

---

## 5. データ依存関係

投入順序は以下のデータ依存関係に基づいています:

```
Tier 1（独立データ — 他に依存しない）
├── 01-auth-seed.sql              → authdb: ユーザー・ロール
├── 02-inventory-categories.sql   → inventorydb: カテゴリ
├── 03-inventory-products.sql     → inventorydb: 商品（カテゴリに依存）
├── 05-coupon-seed.sql            → coupondb: クーポン
└── 08-mail-templates-seed.sql    → mailsenddb: テンプレート

Tier 2（Tier 1 に依存）
├── 07-user-management-seed.sql   → userdb: プロファイル（auth ユーザーに対応）
├── 06-point-seed.sql             → pointdb: ポイント（auth ユーザーに対応）
└── 04-inventory-reviews.sql      → inventorydb: レビュー（商品 + ユーザーに依存）

Tier 3（Tier 1 & 2 に依存）
├── 09-sales-orders-seed.sql      → salesdb: 注文（商品 + ユーザーに依存）
└── 10-cart-seed.sql              → cartdb: カート（商品に依存）
```

---

## 6. 投入データサマリー

### 6.1 テストユーザー（authdb / userdb）

| ID | メール | 名前 | ロール | ランク | パスワード |
|----|-------|------|--------|--------|-----------|
| user-seed-001 | tanaka@example.com | 田中 太郎 | CUSTOMER | PLATINUM | `Password123!` |
| user-seed-002 | sato@example.com | 佐藤 花子 | CUSTOMER | GOLD | `Password123!` |
| user-seed-003 | suzuki@example.com | 鈴木 一郎 | CUSTOMER | SILVER | `Password123!` |
| user-seed-004 | takahashi@example.com | 高橋 美咲 | CUSTOMER | SILVER | `Password123!` |
| user-seed-005 | ito@example.com | 伊藤 健太 | CUSTOMER | SILVER | `Password123!` |
| user-seed-006 | watanabe@example.com | 渡辺 さくら | CUSTOMER | BRONZE | `Password123!` |
| user-seed-007 | yamamoto@example.com | 山本 大輔 | CUSTOMER | BRONZE | `Password123!` |
| user-seed-008 | nakamura@example.com | 中村 優子 | CUSTOMER | BRONZE | `Password123!` |
| user-seed-009 | kobayashi@example.com | 小林 翔太 | CUSTOMER | BRONZE | `Password123!` |
| user-seed-010 | kato@example.com | 加藤 明日香 | CUSTOMER | BRONZE | `Password123!` |

> **注意**: パスワード `Password123!` は開発環境専用です。本番環境では使用しないでください。

### 6.2 商品カテゴリ（inventorydb）

| ID | カテゴリ名 | 商品数 |
|----|-----------|--------|
| cat-ski | スキー板 | 15 |
| cat-boots | スキーブーツ | 15 |
| cat-bindings | ビンディング | 10 |
| cat-poles | ストック・ポール | 8 |
| cat-goggles | ゴーグル | 12 |
| cat-helmets | ヘルメット | 10 |
| cat-gloves | グローブ | 10 |
| cat-jackets | スキージャケット | 8 |
| cat-pants | スキーパンツ | 7 |
| cat-accessories | アクセサリー | 5 |
| **合計** | | **100** |

### 6.3 商品一覧（inventorydb — 100 件）

#### スキー板（15 件）

| SKU | ブランド | 商品名 | 通常価格 | タイプ |
|-----|---------|--------|---------|--------|
| SKI-001 | Salomon | S/Force Bold | ¥98,000 | オールマウンテン |
| SKI-002 | Salomon | QST 98 | ¥110,000 | フリーライド |
| SKI-003 | Atomic | Redster X9S | ¥125,000 | レーシング |
| SKI-004 | Atomic | Maverick 95 TI | ¥95,000 | オールマウンテン |
| SKI-005 | Rossignol | Experience 82 Ti | ¥88,000 | 基礎スキー |
| SKI-006 | Rossignol | Hero Elite ST TI | ¥138,000 | 技術選 |
| SKI-007 | Head | Supershape e-Rally | ¥92,000 | 基礎 |
| SKI-008 | Head | Kore 93 | ¥105,000 | フリーライド |
| SKI-009 | Volkl | Deacon 76 | ¥85,000 | カービング |
| SKI-010 | K2 | Mindbender 99Ti | ¥108,000 | フリーライド |
| SKI-011 | Nordica | Enforcer 100 | ¥115,000 | オールマウンテン |
| SKI-012 | Blizzard | Rustler 9 | ¥102,000 | フリーライド |
| SKI-013 | Fischer | RC One 82 GT | ¥78,000 | 基礎 |
| SKI-014 | Dynastar | Speed Zone 10 TI | ¥82,000 | 基礎 |
| SKI-015 | Line | Blade Optic 96 | ¥99,000 | フリーライド |

#### スキーブーツ（15 件）

| SKU | ブランド | 商品名 | 通常価格 | レベル |
|-----|---------|--------|---------|--------|
| BOOT-001 | Salomon | S/PRO Alpha 120 | ¥78,000 | 上級 |
| BOOT-002 | Salomon | S/PRO MV 100 | ¥58,000 | 中級 |
| BOOT-003 | Atomic | Hawx Ultra 130 | ¥82,000 | 上級 |
| BOOT-004 | Atomic | Hawx Prime 110 | ¥62,000 | 中上級 |
| BOOT-005 | Tecnica | Mach1 MV 130 | ¥85,000 | 上級 |
| BOOT-006 | Tecnica | Cochise 110 | ¥72,000 | フリーライド |
| BOOT-007 | Nordica | Speedmachine 3 130 | ¥79,000 | 上級 |
| BOOT-008 | Nordica | Sportmachine 3 100 | ¥52,000 | 中級 |
| BOOT-009 | Lange | RX 120 | ¥76,000 | 上級 |
| BOOT-010 | Lange | LX 90 | ¥48,000 | 中級 |
| BOOT-011 | Head | Raptor 140 RS | ¥88,000 | レーシング |
| BOOT-012 | Head | Edge 100 | ¥45,000 | 中級 |
| BOOT-013 | Dalbello | DRS 130 | ¥92,000 | レーシング |
| BOOT-014 | K2 | Recon 130 MV | ¥75,000 | 上級 |
| BOOT-015 | Rossignol | Speed 120 HV+ | ¥68,000 | 上級・幅広 |

#### ビンディング（10 件）

| SKU | ブランド | 商品名 | 通常価格 | タイプ |
|-----|---------|--------|---------|--------|
| BIND-001 | Marker | Griffon 13 ID | ¥38,000 | フリーライド |
| BIND-002 | Marker | Squire 11 | ¥25,000 | オールマウンテン |
| BIND-003 | Look | Pivot 14 GW | ¥52,000 | フリーライド |
| BIND-004 | Look | SPX 12 GW | ¥35,000 | 基礎 |
| BIND-005 | Salomon | STH2 WTR 13 | ¥32,000 | オールマウンテン |
| BIND-006 | Salomon | S/LAB Shift MNC 13 | ¥65,000 | ツアー |
| BIND-007 | Tyrolia | Attack² 14 GW | ¥42,000 | オールマウンテン |
| BIND-008 | Tyrolia | Rally 12 GW | ¥28,000 | 基礎 |
| BIND-009 | Atomic | Warden MNC 13 | ¥36,000 | フリーライド |
| BIND-010 | Marker | Jester 16 ID | ¥48,000 | エキスパート |

#### ストック・ポール（8 件）

| SKU | ブランド | 商品名 | 通常価格 |
|-----|---------|--------|---------|
| POLE-001 | Leki | Speed S | ¥18,000 |
| POLE-002 | Leki | Detect S | ¥14,000 |
| POLE-003 | Swix | Sonic R1 | ¥22,000 |
| POLE-004 | Swix | Techlite Pro | ¥12,000 |
| POLE-005 | Black Diamond | Vapor Carbon | ¥28,000 |
| POLE-006 | Scott | Scrapper SRS | ¥16,000 |
| POLE-007 | Komperdell | Nationalteam Carbon | ¥25,000 |
| POLE-008 | Sinano | CX-Falcon | ¥9,800 |

#### ゴーグル（12 件）

| SKU | ブランド | 商品名 | 通常価格 |
|-----|---------|--------|---------|
| GOG-001 | Oakley | Flight Deck L Prizm | ¥32,000 |
| GOG-002 | Oakley | Line Miner L Prizm | ¥28,000 |
| GOG-003 | Smith | I/O MAG ChromaPop | ¥38,000 |
| GOG-004 | Smith | 4D MAG ChromaPop | ¥45,000 |
| GOG-005 | POC | Orb Clarity | ¥25,000 |
| GOG-006 | POC | Zonula Clarity | ¥32,000 |
| GOG-007 | Anon | M4 Toric | ¥35,000 |
| GOG-008 | Anon | M5S | ¥28,000 |
| GOG-009 | Giro | Contour RS Vivid | ¥30,000 |
| GOG-010 | Giro | Axis Vivid | ¥22,000 |
| GOG-011 | Julbo | Aerospace OTG Reactiv | ¥38,000 |
| GOG-012 | SWANS | RIDGELINE | ¥18,000 |

#### ヘルメット（10 件）

| SKU | ブランド | 商品名 | 通常価格 |
|-----|---------|--------|---------|
| HELM-001 | Giro | Tor Spherical MIPS | ¥42,000 |
| HELM-002 | Giro | Neo MIPS | ¥28,000 |
| HELM-003 | Smith | Vantage MIPS | ¥35,000 |
| HELM-004 | Smith | Level MIPS | ¥22,000 |
| HELM-005 | POC | Obex MIPS | ¥25,000 |
| HELM-006 | POC | Skull Dura X MIPS | ¥48,000 |
| HELM-007 | Sweet Protection | Trooper 2Vi MIPS | ¥38,000 |
| HELM-008 | Sweet Protection | Switcher MIPS | ¥32,000 |
| HELM-009 | Oakley | MOD5 MIPS | ¥30,000 |
| HELM-010 | Atomic | Savor AMID | ¥18,000 |

#### グローブ（10 件）

| SKU | ブランド | 商品名 | 通常価格 |
|-----|---------|--------|---------|
| GLOVE-001 | Hestra | Army Leather Heli Ski | ¥25,000 |
| GLOVE-002 | Hestra | Fall Line | ¥22,000 |
| GLOVE-003 | Hestra | Power Heater | ¥32,000 |
| GLOVE-004 | Dakine | Titan GORE-TEX | ¥15,000 |
| GLOVE-005 | Dakine | Sequoia GORE-TEX | ¥14,000 |
| GLOVE-006 | Black Diamond | Guide | ¥28,000 |
| GLOVE-007 | Black Diamond | Mercury Mitt | ¥25,000 |
| GLOVE-008 | Outdoor Research | Carbide Sensor | ¥18,000 |
| GLOVE-009 | SWANY | Toaster | ¥12,000 |
| GLOVE-010 | Reusch | World Cup Warrior | ¥15,000 |

#### スキージャケット（8 件）

| SKU | ブランド | 商品名 | 通常価格 |
|-----|---------|--------|---------|
| JKT-001 | Arc'teryx | Sabre AR Jacket | ¥115,000 |
| JKT-002 | Descente | S.I.O Shell Jacket | ¥88,000 |
| JKT-003 | Goldwin | G-Titan Jacket | ¥95,000 |
| JKT-004 | Phenix | Thunderbolt Jacket | ¥68,000 |
| JKT-005 | Salomon | Brilliant Jacket | ¥42,000 |
| JKT-006 | The North Face | A-CAD FUTURELIGHT | ¥78,000 |
| JKT-007 | Mammut | Stoney HS Jacket | ¥55,000 |
| JKT-008 | Norrøna | lofoten GORE-TEX Pro | ¥108,000 |

#### スキーパンツ（7 件）

| SKU | ブランド | 商品名 | 通常価格 |
|-----|---------|--------|---------|
| PNT-001 | Descente | S.I.O Insulated Pants | ¥65,000 |
| PNT-002 | Goldwin | G-Bliss Pants | ¥72,000 |
| PNT-003 | Phenix | Blizzard Pants | ¥48,000 |
| PNT-004 | Salomon | Brilliant Pants | ¥32,000 |
| PNT-005 | The North Face | Freedom Insulated Pants | ¥38,000 |
| PNT-006 | Arc'teryx | Sabre AR Pants | ¥82,000 |
| PNT-007 | Mammut | Stoney HS Pants | ¥42,000 |

#### アクセサリー（5 件）

| SKU | ブランド | 商品名 | 通常価格 |
|-----|---------|--------|---------|
| ACC-001 | BUFF | Original Multifunctional Headwear | ¥3,500 |
| ACC-002 | Deuter | Race Air Ski Backpack | ¥12,000 |
| ACC-003 | Swix | T77 Economy Waxing Kit | ¥8,500 |
| ACC-004 | GALLIUM | Trial Waxing Set | ¥5,800 |
| ACC-005 | Boot Traction | Walk Soles | ¥3,200 |

### 6.4 クーポン（coupondb）

| コード | 割引 | 最低注文額 | 上限割引額 | 種別 |
|-------|------|----------|----------|------|
| WELCOME10 | 10% OFF | ¥10,000 | ¥5,000 | 新規会員 |
| SPRING2026 | 15% OFF | ¥30,000 | ¥10,000 | 季節セール |
| SKISET3000 | ¥3,000 OFF | ¥50,000 | — | 固定額 |
| VIP20 | 20% OFF | ¥50,000 | ¥20,000 | VIP |
| FLASH5000 | ¥5,000 OFF | ¥30,000 | — | タイムセール |
| BOOTS15 | 15% OFF | ¥20,000 | ¥8,000 | カテゴリ限定 |
| NEWYEAR | ¥2,000 OFF | ¥15,000 | — | 新年セール |
| FRIEND500 | ¥500 OFF | ¥5,000 | — | 紹介 |

### 6.5 注文（salesdb — 10 件）

| 注文番号 | 顧客 | ステータス | 合計金額 |
|---------|------|----------|---------|
| ORD-2026-0001 | 田中太郎 | DELIVERED | 〜¥120,000 |
| ORD-2026-0002 | 佐藤花子 | DELIVERED | 〜¥95,000 |
| ORD-2026-0003 | 鈴木一郎 | DELIVERED | 〜¥85,000 |
| ORD-2026-0004 | 高橋美咲 | SHIPPED | 〜¥68,000 |
| ORD-2026-0005 | 伊藤健太 | SHIPPED | 〜¥52,000 |
| ORD-2026-0006 | 渡辺さくら | CONFIRMED | 〜¥38,000 |
| ORD-2026-0007 | 山本大輔 | CONFIRMED | 〜¥92,000 |
| ORD-2026-0008 | 中村優子 | PENDING | 〜¥45,000 |
| ORD-2026-0009 | 小林翔太 | CANCELLED | 〜¥28,000 |
| ORD-2026-0010 | 加藤明日香 | RETURNED | 〜¥35,000 |

---

## 7. テストアカウントでのログイン方法

サンプルデータ投入後、以下の手順でフロントエンドからログインできます:

```
URL:       http://localhost:3000
メール:     tanaka@example.com
パスワード:  Password123!
```

管理者ポータルには、既存の管理者アカウントでログインしてください:

```
URL:       http://localhost:8081
```

---

## 8. トラブルシューティング

### データ投入エラーが発生した場合

```bash
# 個別の SQL ファイルを直接実行してエラー詳細を確認
docker exec -i skishop-postgres psql -U skishop -d inventorydb -v ON_ERROR_STOP=1 \
  < infra/seed-data/03-inventory-products.sql
```

### シードデータを完全にリセットしたい場合

```bash
./infra/seed-data/seed-all.sh --reset
```

### 特定のテーブルのデータを確認したい場合

```bash
# 商品一覧
docker exec skishop-postgres psql -U skishop -d inventorydb \
  -c "SELECT sku, name, brand FROM products ORDER BY sku LIMIT 20"

# カテゴリ一覧
docker exec skishop-postgres psql -U skishop -d inventorydb \
  -c "SELECT id, name, level FROM categories ORDER BY level, name"

# 注文一覧
docker exec skishop-postgres psql -U skishop -d salesdb \
  -c "SELECT order_number, status, total_amount FROM orders ORDER BY order_number"
```

---

## 9. 注意事項

1. **開発環境専用**: サンプルデータは開発・テスト環境でのみ使用してください
2. **パスワード**: テストパスワード `Password123!` は本番環境では使用しないでください
3. **べき等性**: すべての SQL は `ON CONFLICT DO NOTHING` を使用しているため、何度実行しても安全です
4. **EF Core マイグレーション**: シードスクリプトは EF Core マイグレーション適用後に実行してください
5. **Docker 前提**: PostgreSQL はDocker コンテナ `skishop-postgres` で稼働していることが前提です
