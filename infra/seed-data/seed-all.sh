#!/usr/bin/env bash
# ============================================================
# SkiShop サンプルデータ一括投入スクリプト
#
# 使い方:
#   ./infra/seed-data/seed-all.sh          # 全サービスにデータ投入
#   ./infra/seed-data/seed-all.sh --reset  # 既存シードデータを削除して再投入
#
# 前提条件:
#   - Docker コンテナ (skishop-postgres) が起動していること
#   - docker compose up -d で全サービスが起動済みであること
# ============================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
POSTGRES_CONTAINER="skishop-postgres"
POSTGRES_USER="skishop"

# 色付き出力
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

log_info()  { echo -e "${GREEN}[INFO]${NC}  $1"; }
log_warn()  { echo -e "${YELLOW}[WARN]${NC}  $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# PostgreSQL コンテナの起動確認
check_postgres() {
    if ! docker exec "$POSTGRES_CONTAINER" pg_isready -U "$POSTGRES_USER" > /dev/null 2>&1; then
        log_error "PostgreSQL コンテナ ($POSTGRES_CONTAINER) が起動していません。"
        log_error "先に 'docker compose up -d postgres' を実行してください。"
        exit 1
    fi
    log_info "PostgreSQL コンテナ接続確認 OK"
}

# SQL ファイルの実行
run_sql() {
    local db_name="$1"
    local sql_file="$2"
    local description="$3"

    if [ ! -f "$sql_file" ]; then
        log_warn "ファイルが見つかりません: $sql_file — スキップ"
        return 0
    fi

    log_info "[$db_name] $description ..."
    if docker exec -i "$POSTGRES_CONTAINER" psql -U "$POSTGRES_USER" -d "$db_name" -v ON_ERROR_STOP=1 < "$sql_file" > /dev/null 2>&1; then
        log_info "[$db_name] $description — 完了 ✅"
    else
        log_error "[$db_name] $description — 失敗 ❌"
        log_error "詳細: docker exec -i $POSTGRES_CONTAINER psql -U $POSTGRES_USER -d $db_name < $sql_file"
        return 1
    fi
}

# リセット処理
reset_seed_data() {
    log_warn "=== シードデータのリセットを開始します ==="

    # 各 DB のシードデータを削除（依存関係の逆順で削除）
    local reset_sql
    reset_sql=$(cat << 'RESET_EOF'
-- リセット: シードデータの削除（ON CONFLICT で挿入したデータのみ対象）
-- FK 制約を考慮して子テーブルから削除
RESET_EOF
    )

    # salesdb
    log_info "[salesdb] リセット中..."
    docker exec -i "$POSTGRES_CONTAINER" psql -U "$POSTGRES_USER" -d salesdb -c "
        DELETE FROM order_items WHERE order_id IN (SELECT id FROM orders WHERE order_number LIKE 'ORD-2026-%');
        DELETE FROM orders WHERE order_number LIKE 'ORD-2026-%';
    " > /dev/null 2>&1 || true

    # cartdb
    log_info "[cartdb] リセット中..."
    docker exec -i "$POSTGRES_CONTAINER" psql -U "$POSTGRES_USER" -d cartdb -c "
        DELETE FROM cart_items WHERE cart_id IN (SELECT id FROM carts WHERE id LIKE 'cart-seed-%');
        DELETE FROM carts WHERE id LIKE 'cart-seed-%';
    " > /dev/null 2>&1 || true

    # pointdb
    log_info "[pointdb] リセット中..."
    docker exec -i "$POSTGRES_CONTAINER" psql -U "$POSTGRES_USER" -d pointdb -c "
        DELETE FROM point_transactions WHERE account_id IN (SELECT id FROM point_accounts WHERE id LIKE 'pa-seed-%');
        DELETE FROM point_accounts WHERE id LIKE 'pa-seed-%';
    " > /dev/null 2>&1 || true

    # coupondb
    log_info "[coupondb] リセット中..."
    docker exec -i "$POSTGRES_CONTAINER" psql -U "$POSTGRES_USER" -d coupondb -c "
        DELETE FROM coupon_usages WHERE coupon_id IN (SELECT id FROM coupons WHERE id LIKE 'coupon-seed-%');
        DELETE FROM coupons WHERE id LIKE 'coupon-seed-%';
        DELETE FROM campaigns WHERE id LIKE 'camp-seed-%';
        DELETE FROM coupon_types WHERE id LIKE 'ct-seed-%';
    " > /dev/null 2>&1 || true

    # inventorydb (子テーブルから)
    log_info "[inventorydb] リセット中..."
    docker exec -i "$POSTGRES_CONTAINER" psql -U "$POSTGRES_USER" -d inventorydb -c "
        DELETE FROM review_votes WHERE review_id IN (SELECT id FROM reviews WHERE id LIKE 'rev-seed-%');
        DELETE FROM review_responses WHERE review_id IN (SELECT id FROM reviews WHERE id LIKE 'rev-seed-%');
        DELETE FROM reviews WHERE id LIKE 'rev-seed-%';
        DELETE FROM product_images WHERE product_id LIKE 'prod-%';
        DELETE FROM inventories WHERE product_id LIKE 'prod-%';
        DELETE FROM price_histories WHERE product_id LIKE 'prod-%';
        DELETE FROM prices WHERE product_id LIKE 'prod-%';
        DELETE FROM products WHERE id LIKE 'prod-%';
        DELETE FROM categories WHERE id LIKE 'cat-%';
    " > /dev/null 2>&1 || true

    # userdb
    log_info "[userdb] リセット中..."
    docker exec -i "$POSTGRES_CONTAINER" psql -U "$POSTGRES_USER" -d userdb -c "
        DELETE FROM addresses WHERE user_id LIKE 'user-seed-%';
        DELETE FROM member_ranks WHERE user_id LIKE 'user-seed-%';
        DELETE FROM users WHERE id LIKE 'user-seed-%';
    " > /dev/null 2>&1 || true

    # authdb
    log_info "[authdb] リセット中..."
    docker exec -i "$POSTGRES_CONTAINER" psql -U "$POSTGRES_USER" -d authdb -c "
        DELETE FROM user_roles WHERE user_id LIKE 'user-seed-%';
        DELETE FROM users WHERE id LIKE 'user-seed-%';
    " > /dev/null 2>&1 || true

    # mailsenddb
    log_info "[mailsenddb] リセット中..."
    docker exec -i "$POSTGRES_CONTAINER" psql -U "$POSTGRES_USER" -d mailsenddb -c "
        DELETE FROM mail_templates WHERE id LIKE 'tpl-seed-%';
    " > /dev/null 2>&1 || true

    log_info "=== リセット完了 ==="
    echo ""
}

# メイン処理
main() {
    echo ""
    echo "=============================================="
    echo "  SkiShop サンプルデータ投入スクリプト"
    echo "=============================================="
    echo ""

    # リセットオプション
    if [[ "${1:-}" == "--reset" ]]; then
        check_postgres
        reset_seed_data
    fi

    check_postgres

    echo ""
    log_info "=== サンプルデータの投入を開始します ==="
    echo ""

    # 投入順序: データ依存関係を考慮
    # Tier 1: 独立データ（他に依存しない）
    run_sql "authdb"      "$SCRIPT_DIR/01-auth-seed.sql"              "ユーザー・ロール"
    run_sql "inventorydb" "$SCRIPT_DIR/02-inventory-categories.sql"   "商品カテゴリ"
    run_sql "inventorydb" "$SCRIPT_DIR/03-inventory-products.sql"     "商品・価格・在庫（100件）"
    run_sql "coupondb"    "$SCRIPT_DIR/05-coupon-seed.sql"            "クーポン・キャンペーン"
    run_sql "mailsenddb"  "$SCRIPT_DIR/08-mail-templates-seed.sql"    "メールテンプレート"

    # Tier 2: Tier 1 に依存するデータ
    run_sql "userdb"      "$SCRIPT_DIR/07-user-management-seed.sql"   "ユーザープロファイル・住所"
    run_sql "pointdb"     "$SCRIPT_DIR/06-point-seed.sql"             "ポイントアカウント"
    run_sql "inventorydb" "$SCRIPT_DIR/04-inventory-reviews.sql"      "商品レビュー（30件）"

    # Tier 3: Tier 1 & 2 に依存するデータ
    run_sql "salesdb"     "$SCRIPT_DIR/09-sales-orders-seed.sql"      "注文データ（10件）"
    run_sql "cartdb"      "$SCRIPT_DIR/10-cart-seed.sql"              "カートデータ"

    echo ""
    log_info "=== サンプルデータの投入が完了しました 🎉 ==="
    echo ""

    # 投入結果サマリー
    echo "--- 投入結果サマリー ---"
    for db in authdb inventorydb coupondb pointdb userdb mailsenddb salesdb cartdb; do
        echo ""
        echo "[$db]"
        tables=$(docker exec "$POSTGRES_CONTAINER" psql -U "$POSTGRES_USER" -d "$db" -t -c \
            "SELECT tablename FROM pg_tables WHERE schemaname='public' AND tablename != '__EFMigrationsHistory'" 2>/dev/null)
        for table in $tables; do
            table=$(echo "$table" | xargs)
            if [ -n "$table" ]; then
                count=$(docker exec "$POSTGRES_CONTAINER" psql -U "$POSTGRES_USER" -d "$db" -t -c \
                    "SELECT count(*) FROM \"$table\"" 2>/dev/null | xargs)
                printf "  %-30s %s rows\n" "$table" "$count"
            fi
        done
    done

    echo ""
    echo "=============================================="
    echo "  完了！ブラウザで http://localhost:3000 を開いて確認してください"
    echo "=============================================="
}

main "$@"
