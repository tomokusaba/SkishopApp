namespace AuthService.DTOs.Responses;

/// <summary>
/// 汎用メッセージレスポンス DTO。
/// 操作の成功・失敗を示すシンプルなメッセージを返す際に使用する。
/// パスワードリセット要求、メール確認、ログアウト等の操作結果に使用される。
/// </summary>
/// <param name="Message">操作結果を示すメッセージ。ユーザーに表示可能な形式。</param>
public record MessageResponse(string Message);
