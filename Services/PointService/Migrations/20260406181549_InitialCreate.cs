using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PointService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "outbox_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", maxLength: 36, nullable: false),
                    aggregate_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    aggregate_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "PENDING"),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_events", x => x.id);
                    table.CheckConstraint("ck_outbox_events_retry_count", "retry_count >= 0");
                    table.CheckConstraint("ck_outbox_events_status", "status IN ('PENDING','PUBLISHED','FAILED')");
                });

            migrationBuilder.CreateTable(
                name: "point_accounts",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    available_points = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    pending_points = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    total_earned = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    total_spent = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    total_expired = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_point_accounts", x => x.id);
                    table.CheckConstraint("ck_point_accounts_available_points", "available_points >= 0");
                });

            migrationBuilder.CreateTable(
                name: "point_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", maxLength: 36, nullable: false),
                    admin_user_id = table.Column<Guid>(type: "uuid", maxLength: 36, nullable: false),
                    target_user_id = table.Column<Guid>(type: "uuid", maxLength: 36, nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    points_before = table.Column<int>(type: "integer", nullable: false),
                    points_after = table.Column<int>(type: "integer", nullable: false),
                    points_changed = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_point_audit_logs", x => x.id);
                    table.CheckConstraint("ck_point_audit_logs_action", "action IN ('ADD','SUBTRACT','ADJUST')");
                });

            migrationBuilder.CreateTable(
                name: "point_campaigns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", maxLength: 36, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    multiplier = table.Column<decimal>(type: "numeric", nullable: false, defaultValue: 1.0m),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_point_campaigns", x => x.id);
                    table.CheckConstraint("ck_point_campaigns_dates", "end_date > start_date");
                    table.CheckConstraint("ck_point_campaigns_multiplier", "multiplier > 0");
                });

            migrationBuilder.CreateTable(
                name: "point_conversion_rates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", maxLength: 36, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    rate_per_point = table.Column<decimal>(type: "numeric", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_point_conversion_rates", x => x.id);
                    table.CheckConstraint("ck_point_conversion_rates_rate", "rate_per_point > 0");
                });

            migrationBuilder.CreateTable(
                name: "point_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", maxLength: 36, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    point_rate = table.Column<decimal>(type: "numeric", nullable: false),
                    minimum_amount = table.Column<decimal>(type: "numeric", nullable: false, defaultValue: 0m),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_point_rules", x => x.id);
                    table.CheckConstraint("ck_point_rules_minimum_amount", "minimum_amount >= 0");
                });

            migrationBuilder.CreateTable(
                name: "tier_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", maxLength: 36, nullable: false),
                    name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    point_rate = table.Column<decimal>(type: "numeric", nullable: false),
                    min_annual_points = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    benefits = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tier_definitions", x => x.id);
                    table.CheckConstraint("ck_tier_definitions_name", "name IN ('BRONZE','SILVER','GOLD','PLATINUM')");
                    table.CheckConstraint("ck_tier_definitions_point_rate", "point_rate > 0 AND point_rate <= 1");
                });

            migrationBuilder.CreateTable(
                name: "point_transactions",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    account_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    balance_after = table.Column<int>(type: "integer", nullable: false),
                    reference_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    reference_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_point_transactions", x => x.id);
                    table.CheckConstraint("ck_point_transactions_points", "points != 0");
                    table.CheckConstraint("ck_point_transactions_type", "type IN ('EARN','REDEEM','EXPIRE','ADJUST','RESERVE','RELEASE','REFUND','CANCEL')");
                    table.ForeignKey(
                        name: "FK_point_transactions_point_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "point_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "point_expiries",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    account_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                    source_transaction_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_point_expiries", x => x.id);
                    table.CheckConstraint("ck_point_expiries_points", "points > 0");
                    table.CheckConstraint("ck_point_expiries_status", "status IN ('ACTIVE','EXPIRED','CONSUMED')");
                    table.ForeignKey(
                        name: "FK_point_expiries_point_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "point_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_point_expiries_point_transactions_source_transaction_id",
                        column: x => x.source_transaction_id,
                        principalTable: "point_transactions",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "idx_outbox_pending",
                table: "outbox_events",
                columns: new[] { "status", "created_at" },
                filter: "status = 'PENDING'");

            migrationBuilder.CreateIndex(
                name: "IX_point_accounts_user_id",
                table: "point_accounts",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_audit_admin_user_id",
                table: "point_audit_logs",
                column: "admin_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_audit_created_at",
                table: "point_audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_audit_target_user_id",
                table: "point_audit_logs",
                column: "target_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_campaign_active_dates",
                table: "point_campaigns",
                columns: new[] { "start_date", "end_date" },
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "IX_point_conversion_rates_currency_code",
                table: "point_conversion_rates",
                column: "currency_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_point_expiry_account_id",
                table: "point_expiries",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "idx_point_expiry_user_expires",
                table: "point_expiries",
                columns: new[] { "user_id", "expires_at" },
                filter: "status = 'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "IX_point_expiries_source_transaction_id",
                table: "point_expiries",
                column: "source_transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_point_rules_name",
                table: "point_rules",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_point_tx_account_id",
                table: "point_transactions",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "idx_point_tx_created_at",
                table: "point_transactions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_point_tx_reference",
                table: "point_transactions",
                columns: new[] { "reference_id", "reference_type" });

            migrationBuilder.CreateIndex(
                name: "idx_point_tx_user_id",
                table: "point_transactions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_tier_definitions_name",
                table: "tier_definitions",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tier_definitions_sort_order",
                table: "tier_definitions",
                column: "sort_order");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_events");

            migrationBuilder.DropTable(
                name: "point_audit_logs");

            migrationBuilder.DropTable(
                name: "point_campaigns");

            migrationBuilder.DropTable(
                name: "point_conversion_rates");

            migrationBuilder.DropTable(
                name: "point_expiries");

            migrationBuilder.DropTable(
                name: "point_rules");

            migrationBuilder.DropTable(
                name: "tier_definitions");

            migrationBuilder.DropTable(
                name: "point_transactions");

            migrationBuilder.DropTable(
                name: "point_accounts");
        }
    }
}
