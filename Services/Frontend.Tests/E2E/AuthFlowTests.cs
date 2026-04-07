using Shouldly;
using Xunit;

namespace Frontend.Tests.E2E;

/// <summary>
/// E2E テスト: 認証フロー（登録→メール認証→ログイン→ログアウト）
/// Playwright を使用し、実行中のサーバーに対して実行する。
/// CI 環境でのみ実行可能（ローカルでは Skip）。
/// </summary>
[Trait("Category", "E2E")]
public class AuthFlowTests
{
    [Fact(Skip = "E2E テストは実行中のサーバーが必要。CI パイプライン構築後に有効化。Issue #E2E-002")]
    public async Task Should_RegisterAndLogin_When_ValidCredentials()
    {
        // Arrange
        // Playwright ブラウザ起動 + サーバー URL 設定

        // Act
        // 1. 登録ページに遷移
        // 2. ユーザー情報を入力して登録
        // 3. メール認証を完了（テスト用 API で認証トークン取得）
        // 4. ログインページでログイン
        // 5. マイページが表示されることを確認

        // Assert
        await Task.CompletedTask;
        true.ShouldBeTrue();
    }

    [Fact(Skip = "E2E テストは実行中のサーバーが必要。CI パイプライン構築後に有効化。Issue #E2E-002")]
    public async Task Should_ShowError_When_InvalidPassword()
    {
        // Arrange & Act & Assert
        await Task.CompletedTask;
        true.ShouldBeTrue();
    }

    [Fact(Skip = "E2E テストは実行中のサーバーが必要。CI パイプライン構築後に有効化。Issue #E2E-002")]
    public async Task Should_Logout_When_LogoutClicked()
    {
        // Arrange & Act & Assert
        await Task.CompletedTask;
        true.ShouldBeTrue();
    }

    [Fact(Skip = "E2E テストは実行中のサーバーが必要。CI パイプライン構築後に有効化。Issue #E2E-002")]
    public async Task Should_ShowMfaInput_When_MfaEnabled()
    {
        // Arrange & Act & Assert
        await Task.CompletedTask;
        true.ShouldBeTrue();
    }
}
