namespace AuthService.Enums;

/// <summary>
/// ユーザーの権限レベルを表すロール種別の列挙型。
/// RBAC（ロールベースアクセス制御）の基盤として使用される。
/// </summary>
/// <remarks>
/// <para>
/// 権限の強さ: Admin &gt; Manager &gt; Staff &gt; Employee &gt; User &gt; Customer
/// </para>
/// <para>
/// 管理者権限（Admin, Manager）を持つユーザーの操作は全て監査ログに記録される。
/// ロールの変更には管理者承認が必要。
/// </para>
/// </remarks>
public enum UserRoleType
{
    /// <summary>
    /// システム管理者。全ての管理機能へのフルアクセス権限を持つ。ユーザー管理、システム設定の変更が可能。
    /// </summary>
    Admin,

    /// <summary>
    /// マネージャー。Staff および Employee の管理権限を持つ。在庫・販売管理のフル操作が可能。
    /// </summary>
    Manager,

    /// <summary>
    /// スタッフ。店舗運営に必要な操作権限を持つ。注文処理、在庫確認が可能。
    /// </summary>
    Staff,

    /// <summary>
    /// 従業員。基本的な業務操作のみ可能。限定的なアクセス権限。
    /// </summary>
    Employee,

    /// <summary>
    /// 一般ユーザー。自身のプロファイル管理と注文履歴の参照が可能。
    /// </summary>
    User,

    /// <summary>
    /// 顧客。EC サイトでの商品購入、カート操作、注文が可能。デフォルトのロール。
    /// </summary>
    Customer
}
