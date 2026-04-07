using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiSupportService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "demand_forecasts",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    product_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    forecast_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    forecast_period = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    predicted_demand = table.Column<int>(type: "integer", nullable: false),
                    confidence_score = table.Column<decimal>(type: "numeric", nullable: false),
                    model_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_demand_forecasts", x => x.id);
                    table.CheckConstraint("ck_demand_forecasts_confidence", "confidence_score >= 0 AND confidence_score <= 1");
                    table.CheckConstraint("ck_demand_forecasts_predicted_demand", "predicted_demand >= 0");
                });

            migrationBuilder.CreateTable(
                name: "model_trainings",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    model_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    metrics_json = table.Column<string>(type: "jsonb", nullable: true),
                    parameters_json = table.Column<string>(type: "jsonb", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_model_trainings", x => x.id);
                    table.CheckConstraint("ck_model_trainings_status", "status IN ('PENDING', 'TRAINING', 'COMPLETED', 'FAILED')");
                });

            migrationBuilder.CreateTable(
                name: "outbox_events",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    aggregate_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    aggregate_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    topic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_events", x => x.id);
                    table.CheckConstraint("ck_outbox_events_retry_count", "retry_count >= 0");
                    table.CheckConstraint("ck_outbox_events_status", "status IN ('PENDING', 'PUBLISHED', 'FAILED')");
                });

            migrationBuilder.CreateTable(
                name: "search_analytics",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    user_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    query = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    search_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    results_count = table.Column<int>(type: "integer", nullable: false),
                    clicked_product_ids_json = table.Column<string>(type: "jsonb", nullable: true),
                    response_time_ms = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_analytics", x => x.id);
                    table.CheckConstraint("ck_search_analytics_search_type", "search_type IN ('KEYWORD', 'SEMANTIC', 'HYBRID')");
                });

            migrationBuilder.CreateTable(
                name: "user_profiles",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    user_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    preferences_json = table.Column<string>(type: "jsonb", nullable: true),
                    browsing_history_json = table.Column<string>(type: "jsonb", nullable: true),
                    purchase_history_json = table.Column<string>(type: "jsonb", nullable: true),
                    last_activity_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_profiles", x => x.id);
                    table.UniqueConstraint("AK_user_profiles_user_id", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "chat_sessions",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    user_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    context_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    user_profile_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_sessions", x => x.id);
                    table.CheckConstraint("ck_chat_sessions_status", "status IN ('ACTIVE', 'CLOSED', 'ESCALATED')");
                    table.ForeignKey(
                        name: "FK_chat_sessions_user_profiles_user_profile_id",
                        column: x => x.user_profile_id,
                        principalTable: "user_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "recommendations",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    user_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    product_ids_json = table.Column<string>(type: "jsonb", nullable: false),
                    score = table.Column<decimal>(type: "numeric", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_viewed = table.Column<bool>(type: "boolean", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendations", x => x.id);
                    table.CheckConstraint("ck_recommendations_type", "type IN ('PERSONALIZED', 'TRENDING', 'SIMILAR', 'SEASONAL', 'FREQUENTLY_BOUGHT_TOGETHER')");
                    table.ForeignKey(
                        name: "FK_recommendations_user_profiles_user_id",
                        column: x => x.user_id,
                        principalTable: "user_profiles",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chat_messages",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    session_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: true),
                    token_count = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_messages", x => x.id);
                    table.CheckConstraint("ck_chat_messages_role", "role IN ('user', 'assistant', 'system')");
                    table.ForeignKey(
                        name: "FK_chat_messages_chat_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "chat_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_chat_messages_session_created",
                table: "chat_messages",
                columns: new[] { "session_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_chat_messages_session_id",
                table: "chat_messages",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "idx_chat_sessions_user_id",
                table: "chat_sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_chat_sessions_user_status",
                table: "chat_sessions",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_sessions_user_profile_id",
                table: "chat_sessions",
                column: "user_profile_id");

            migrationBuilder.CreateIndex(
                name: "idx_demand_forecasts_product_date",
                table: "demand_forecasts",
                columns: new[] { "product_id", "forecast_date" });

            migrationBuilder.CreateIndex(
                name: "idx_demand_forecasts_product_id",
                table: "demand_forecasts",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "idx_demand_forecasts_product_period",
                table: "demand_forecasts",
                columns: new[] { "product_id", "forecast_period" });

            migrationBuilder.CreateIndex(
                name: "idx_model_trainings_model_name",
                table: "model_trainings",
                column: "model_name");

            migrationBuilder.CreateIndex(
                name: "idx_outbox_events_aggregate",
                table: "outbox_events",
                columns: new[] { "aggregate_type", "aggregate_id" });

            migrationBuilder.CreateIndex(
                name: "idx_outbox_events_status_created",
                table: "outbox_events",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_recommendations_expires",
                table: "recommendations",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_recommendations_user_id",
                table: "recommendations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_recommendations_user_type",
                table: "recommendations",
                columns: new[] { "user_id", "type" });

            migrationBuilder.CreateIndex(
                name: "idx_search_analytics_created_at",
                table: "search_analytics",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_search_analytics_query",
                table: "search_analytics",
                column: "query");

            migrationBuilder.CreateIndex(
                name: "idx_search_analytics_user_id",
                table: "search_analytics",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_user_profiles_last_activity",
                table: "user_profiles",
                column: "last_activity_at");

            migrationBuilder.CreateIndex(
                name: "uq_user_profiles_user_id",
                table: "user_profiles",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_messages");

            migrationBuilder.DropTable(
                name: "demand_forecasts");

            migrationBuilder.DropTable(
                name: "model_trainings");

            migrationBuilder.DropTable(
                name: "outbox_events");

            migrationBuilder.DropTable(
                name: "recommendations");

            migrationBuilder.DropTable(
                name: "search_analytics");

            migrationBuilder.DropTable(
                name: "chat_sessions");

            migrationBuilder.DropTable(
                name: "user_profiles");
        }
    }
}
