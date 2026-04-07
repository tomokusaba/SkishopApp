using PointService.Models;
using Shouldly;

namespace PointService.Tests.Models;

public class PointAccountTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Should_UpdateTotals_When_EarnReserveAndConfirmAreExecuted()
    {
        // Arrange
        var account = new PointAccount { UserId = "user-1" };

        // Act
        account.EarnPoints(100);
        account.ReservePoints(40);
        account.ConfirmReservation(40);

        // Assert
        account.AvailablePoints.ShouldBe(60);
        account.PendingPoints.ShouldBe(0);
        account.TotalEarned.ShouldBe(100);
        account.TotalSpent.ShouldBe(40);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_RestoreAvailablePoints_When_ReservationIsReleased()
    {
        // Arrange
        var account = new PointAccount { UserId = "user-1" };
        account.EarnPoints(100);
        account.ReservePoints(30);

        // Act
        account.ReleaseReservation(30);

        // Assert
        account.AvailablePoints.ShouldBe(100);
        account.PendingPoints.ShouldBe(0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_ClearBalanceAndReplaceUserId_When_Anonymized()
    {
        // Arrange
        var account = new PointAccount { UserId = "user-1" };
        account.EarnPoints(80);
        account.ReservePoints(20);

        // Act
        account.Anonymize("anon-user");

        // Assert
        account.UserId.ShouldBe("anon-user");
        account.AvailablePoints.ShouldBe(0);
        account.PendingPoints.ShouldBe(0);
    }
}
