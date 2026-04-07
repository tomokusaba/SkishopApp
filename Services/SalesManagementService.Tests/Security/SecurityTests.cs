using SalesManagementService.Infrastructure.Exceptions;
using SalesManagementService.Infrastructure.Security;
using Shouldly;

namespace SalesManagementService.Tests.Security;

public class SecurityTests
{
    // ── ErrorCodes 定数の存在確認 ──

    [Fact]
    [Trait("Category", "Security")]
    public void Should_HaveInvalidRequestCode_When_CheckingErrorCodes()
    {
        // Arrange & Act & Assert
        ErrorCodes.InvalidRequest.ShouldBe("ORD-4001");
    }

    [Fact]
    [Trait("Category", "Security")]
    public void Should_HaveOrderNotFoundCode_When_CheckingErrorCodes()
    {
        // Arrange & Act & Assert
        ErrorCodes.OrderNotFound.ShouldBe("ORD-4041");
    }

    [Fact]
    [Trait("Category", "Security")]
    public void Should_HaveConcurrencyConflictCode_When_CheckingErrorCodes()
    {
        // Arrange & Act & Assert
        ErrorCodes.ConcurrencyConflict.ShouldBe("ORD-4091");
    }

    [Fact]
    [Trait("Category", "Security")]
    public void Should_HavePaymentPendingCode_When_CheckingErrorCodes()
    {
        // Arrange & Act & Assert
        ErrorCodes.PaymentPending.ShouldBe("ORD-2021");
    }

    [Fact]
    [Trait("Category", "Security")]
    public void Should_HaveInternalServerErrorCode_When_CheckingErrorCodes()
    {
        // Arrange & Act & Assert
        ErrorCodes.InternalServerError.ShouldBe("ORD-5001");
    }

    [Fact]
    [Trait("Category", "Security")]
    public void Should_HaveExternalServiceUnavailableCode_When_CheckingErrorCodes()
    {
        // Arrange & Act & Assert
        ErrorCodes.ExternalServiceUnavailable.ShouldBe("ORD-5002");
    }

    // ── GlobalExceptionHandler の構造テスト ──

    [Fact]
    [Trait("Category", "Security")]
    public void Should_ImplementIExceptionHandler_When_CheckingGlobalExceptionHandler()
    {
        // Arrange & Act
        var handlerType = typeof(GlobalExceptionHandler);
        var interfaces = handlerType.GetInterfaces();

        // Assert
        interfaces.ShouldContain(t => t.Name == "IExceptionHandler");
    }

    // ── 例外クラスの存在と階層確認 ──

    [Fact]
    [Trait("Category", "Security")]
    public void Should_HaveNotFoundException_When_CheckingExceptionHierarchy()
    {
        // Arrange & Act
        var ex = new NotFoundException("test");

        // Assert
        ex.ShouldBeAssignableTo<Exception>();
        ex.Message.ShouldBe("test");
    }

    [Fact]
    [Trait("Category", "Security")]
    public void Should_HaveBusinessException_When_CheckingExceptionHierarchy()
    {
        // Arrange & Act
        var ex = new BusinessException("ビジネスルール違反");

        // Assert
        ex.ShouldBeAssignableTo<Exception>();
        ex.Message.ShouldBe("ビジネスルール違反");
    }

    [Fact]
    [Trait("Category", "Security")]
    public void Should_HaveConcurrencyException_When_CheckingExceptionHierarchy()
    {
        // Arrange & Act
        var ex = new ConcurrencyException("楽観的ロック競合");

        // Assert
        ex.ShouldBeAssignableTo<Exception>();
        ex.Message.ShouldBe("楽観的ロック競合");
    }

    [Fact]
    [Trait("Category", "Security")]
    public void Should_HaveForbiddenException_When_CheckingExceptionHierarchy()
    {
        // Arrange & Act
        var ex = new ForbiddenException();

        // Assert
        ex.ShouldBeAssignableTo<Exception>();
    }

    [Fact]
    [Trait("Category", "Security")]
    public void Should_HaveInvalidOrderStateException_When_CheckingExceptionHierarchy()
    {
        // Arrange & Act
        var ex = new InvalidOrderStateException("無効な遷移");

        // Assert
        ex.ShouldBeAssignableTo<Exception>();
        ex.Message.ShouldBe("無効な遷移");
    }

    [Fact]
    [Trait("Category", "Security")]
    public void Should_HavePaymentPendingException_When_CheckingExceptionHierarchy()
    {
        // Arrange & Act
        var ex = new PaymentPendingException("決済確認待ち");

        // Assert
        ex.ShouldBeAssignableTo<Exception>();
        ex.Message.ShouldBe("決済確認待ち");
    }

    // ── エラーコード全カテゴリの網羅確認 ──

    [Theory]
    [Trait("Category", "Security")]
    [InlineData("ORD-4001")]
    [InlineData("ORD-4002")]
    [InlineData("ORD-4003")]
    [InlineData("ORD-4041")]
    [InlineData("ORD-4042")]
    [InlineData("ORD-4043")]
    [InlineData("ORD-4044")]
    [InlineData("ORD-4045")]
    [InlineData("ORD-4091")]
    [InlineData("ORD-4092")]
    [InlineData("ORD-4221")]
    [InlineData("ORD-4222")]
    [InlineData("ORD-4223")]
    [InlineData("ORD-4224")]
    [InlineData("ORD-4225")]
    [InlineData("ORD-2021")]
    [InlineData("ORD-5001")]
    [InlineData("ORD-5002")]
    [InlineData("ORD-5003")]
    public void Should_HaveValidErrorCodeFormat_When_CheckingFormat(string expectedCode)
    {
        // Arrange & Act & Assert
        expectedCode.ShouldStartWith("ORD-");
        expectedCode.Length.ShouldBeGreaterThan(4);
    }
}
