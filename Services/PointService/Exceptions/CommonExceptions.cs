namespace PointService.Exceptions;

public class NotFoundException(string message) : Exception(message);
public class BusinessException(string message) : Exception(message);
public class UnauthorizedException() : Exception("認証が必要です");
public class ForbiddenException() : Exception("アクセスが拒否されました");
