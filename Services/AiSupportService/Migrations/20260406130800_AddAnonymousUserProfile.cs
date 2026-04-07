using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace AiSupportService.Migrations;

/// <summary>
/// 匿名ユーザー向けレコメンデーション用のユーザープロファイルを追加するマイグレーション。
/// </summary>
/// <remarks>
/// <para>
/// trending、seasonal、similar、frequently-bought などの公開レコメンデーションは
/// 特定のユーザーに紐付けられないため、"anonymous" ユーザープロファイルを使用します。
/// </para>
/// <para>
/// このマイグレーションは recommendations テーブルの外部キー制約
/// （FK_recommendations_user_profiles_user_id）を満たすために必要です。
/// </para>
/// </remarks>
public partial class AddAnonymousUserProfile : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 匿名ユーザープロファイルを挿入
        // ON CONFLICT (user_id) DO NOTHING で冪等性を確保
        migrationBuilder.Sql("""
            INSERT INTO user_profiles (
                id,
                user_id,
                preferences_json,
                browsing_history_json,
                purchase_history_json,
                last_activity_at,
                created_at,
                updated_at,
                row_version
            ) VALUES (
                '00000000-0000-0000-0000-000000000001',
                'anonymous',
                '{"type": "anonymous", "description": "匿名ユーザー向けレコメンデーション用プロファイル"}',
                '[]',
                '[]',
                NULL,
                CURRENT_TIMESTAMP,
                CURRENT_TIMESTAMP,
                '\x00000000'::bytea
            )
            ON CONFLICT (user_id) DO NOTHING;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // 匿名ユーザープロファイルを削除
        // 関連する recommendations は ON DELETE CASCADE で自動削除される
        migrationBuilder.Sql("DELETE FROM user_profiles WHERE user_id = 'anonymous';");
    }
}
