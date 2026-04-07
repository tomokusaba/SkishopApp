namespace AiSupportService.Exceptions;

/// <summary>
/// AiSupportService のドメイン例外の基底クラス。
/// </summary>
public class AiSupportException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>
/// 指定されたリソースが見つからない場合にスローされる例外。HTTP 404 にマッピングされる。
/// </summary>
public class NotFoundException(string message)
    : AiSupportException(message);

/// <summary>
/// ビジネスルール違反が検出された場合にスローされる例外。HTTP 422 にマッピングされる。
/// </summary>
public class BusinessException(string message)
    : AiSupportException(message);

/// <summary>
/// 認証されていないリクエストに対してスローされる例外。HTTP 401 にマッピングされる。
/// </summary>
public class UnauthorizedException(string message = "認証が必要です")
    : AiSupportException(message);

/// <summary>
/// 認証済みだがリソースへのアクセス権限がない場合にスローされる例外。HTTP 403 にマッピングされる。
/// </summary>
public class ForbiddenException(string message = "アクセスが拒否されました")
    : AiSupportException(message);

/// <summary>
/// 楽観的ロックの競合が発生した場合にスローされる例外。HTTP 409 にマッピングされる。
/// </summary>
public class ConcurrencyException : AiSupportException
{
    public ConcurrencyException(string message) : base(message) { }
    public ConcurrencyException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// セキュリティポリシー違反（不正な入力等）が検出された場合にスローされる例外。HTTP 400 にマッピングされる。
/// </summary>
public class SecurityException(string message)
    : AiSupportException(message);

/// <summary>
/// Azure OpenAI 等の AI バックエンドサービスが一時的に利用不可の場合にスローされる例外。HTTP 503 にマッピングされる。
/// </summary>
public class AiServiceUnavailableException(string message, Exception? innerException = null)
    : AiSupportException(message, innerException);

/// <summary>
/// AI モデルのトークン上限を超過した場合にスローされる例外。HTTP 422 にマッピングされる。
/// </summary>
public class TokenLimitExceededException(string message)
    : BusinessException(message);

/// <summary>
/// ユーザーあたりのセッション数上限を超過した場合にスローされる例外。HTTP 422 にマッピングされる。
/// </summary>
public class SessionLimitExceededException(string message)
    : BusinessException(message);
