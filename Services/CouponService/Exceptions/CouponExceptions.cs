namespace CouponService.Exceptions;

public abstract class CouponServiceException(string message) : Exception(message);

public class CouponNotFoundException(string message) : CouponServiceException(message);

public class CouponExpiredException(string message) : CouponServiceException(message);

public class CouponUsageLimitExceededException(string message) : CouponServiceException(message);

public class InvalidCouponException(string message) : CouponServiceException(message);

public class CouponFraudDetectedException(string message) : CouponServiceException(message);

public class CouponAlreadyAcquiredException(string message) : CouponServiceException(message);

public class CampaignIssueLimitReachedException(string message) : CouponServiceException(message);

public class UnauthorizedException(string message = "認証が必要です") : CouponServiceException(message);

public class ForbiddenException() : CouponServiceException("アクセスが拒否されました");

public class ConcurrencyException(string message) : CouponServiceException(message);

public class BusinessException : CouponServiceException
{
    public BusinessException(string message) : base(message) { }
}
