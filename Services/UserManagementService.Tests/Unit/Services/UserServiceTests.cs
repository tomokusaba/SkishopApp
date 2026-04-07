using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Microsoft.Extensions.Logging;
using UserManagementService.DTOs.Requests;
using UserManagementService.DTOs.Responses;
using UserManagementService.Exceptions;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;
using UserManagementService.Services;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.Tests.Unit.Services;

public class UserServiceTests
{
    private readonly IUserRepository _userRepository;
    private readonly IPreferenceRepository _preferenceRepository;
    private readonly IMemberRankRepository _memberRankRepository;
    private readonly IActivityRepository _activityRepository;
    private readonly IEventPublisherService _eventPublisher;
    private readonly ICacheService _cacheService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UserService> _logger;
    private readonly UserService _sut;

    private static readonly DateTimeOffset FixedNow =
        new(2026, 3, 18, 12, 0, 0, TimeSpan.Zero);

    public UserServiceTests()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _preferenceRepository = Substitute.For<IPreferenceRepository>();
        _memberRankRepository = Substitute.For<IMemberRankRepository>();
        _activityRepository = Substitute.For<IActivityRepository>();
        _eventPublisher = Substitute.For<IEventPublisherService>();
        _cacheService = Substitute.For<ICacheService>();
        _timeProvider = Substitute.For<TimeProvider>();
        _logger = Substitute.For<ILogger<UserService>>();

        _timeProvider.GetUtcNow().Returns(FixedNow);

        _sut = new UserService(
            _userRepository,
            _preferenceRepository,
            _memberRankRepository,
            _activityRepository,
            _eventPublisher,
            _cacheService,
            _timeProvider,
            _logger);
    }

    private static User CreateTestUser(string id = "user-1") => new()
    {
        Id = id,
        Email = "test@example.com",
        FirstName = "太郎",
        LastName = "テスト",
        PhoneNumber = "090-1234-5678",
        BirthDate = new DateOnly(1990, 1, 15),
        Status = UserStatus.Active,
        IsProcessingRestricted = false,
        LastLoginAt = FixedNow.AddDays(-1),
        CreatedAt = FixedNow.AddMonths(-6),
        UpdatedAt = FixedNow.AddDays(-1)
    };

    #region GetByIdAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnCachedUser_When_CacheHit()
    {
        // Arrange
        var cachedDto = new UserDto("user-1", "test@example.com", "太郎", "テスト",
            "090-1234-5678", new DateOnly(1990, 1, 15), UserStatus.Active,
            false, FixedNow.AddDays(-1), FixedNow.AddMonths(-6), FixedNow.AddDays(-1));
        _cacheService.GetAsync<UserDto>("user:profile:user-1", Arg.Any<CancellationToken>())
            .Returns(cachedDto);

        // Act
        var result = await _sut.GetByIdAsync("user-1");

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("user-1");
        result.Email.ShouldBe("test@example.com");
        await _userRepository.DidNotReceive().FindByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnUserFromRepository_When_CacheMiss()
    {
        // Arrange
        _cacheService.GetAsync<UserDto>("user:profile:user-1", Arg.Any<CancellationToken>())
            .Returns((UserDto?)null);
        var user = CreateTestUser();
        _userRepository.FindByIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        var result = await _sut.GetByIdAsync("user-1");

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("user-1");
        result.FirstName.ShouldBe("太郎");
        await _cacheService.Received(1).SetAsync(
            "user:profile:user-1",
            Arg.Any<UserDto>(),
            TimeSpan.FromMinutes(30),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnNull_When_UserNotFoundAndCacheMiss()
    {
        // Arrange
        _cacheService.GetAsync<UserDto>("user:profile:user-999", Arg.Any<CancellationToken>())
            .Returns((UserDto?)null);
        _userRepository.FindByIdAsync("user-999", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var result = await _sut.GetByIdAsync("user-999");

        // Assert
        result.ShouldBeNull();
        await _cacheService.DidNotReceive().SetAsync(
            Arg.Any<string>(), Arg.Any<UserDto>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region UpdateProfileAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_UpdateAllFields_When_AllFieldsProvided()
    {
        // Arrange
        var user = CreateTestUser();
        _userRepository.FindByIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(user);
        var request = new UpdateUserRequest("花子", "更新", "080-9876-5432", new DateOnly(1995, 5, 20));

        // Act
        var result = await _sut.UpdateProfileAsync("user-1", request);

        // Assert
        result.ShouldNotBeNull();
        result.FirstName.ShouldBe("花子");
        result.LastName.ShouldBe("更新");
        result.PhoneNumber.ShouldBe("080-9876-5432");
        result.BirthDate.ShouldBe(new DateOnly(1995, 5, 20));
        await _eventPublisher.Received(1).PublishProfileUpdatedAsync("user-1", Arg.Any<CancellationToken>());
        await _userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _cacheService.Received(1).RemoveAsync("user:profile:user-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_UpdateOnlyProvidedFields_When_PartialUpdate()
    {
        // Arrange
        var user = CreateTestUser();
        _userRepository.FindByIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(user);
        var request = new UpdateUserRequest("更新名", null, null, null);

        // Act
        var result = await _sut.UpdateProfileAsync("user-1", request);

        // Assert
        result.FirstName.ShouldBe("更新名");
        result.LastName.ShouldBe("テスト");
        result.PhoneNumber.ShouldBe("090-1234-5678");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowNotFoundException_When_UserNotFoundOnUpdate()
    {
        // Arrange
        _userRepository.FindByIdAsync("no-user", Arg.Any<CancellationToken>())
            .Returns((User?)null);
        var request = new UpdateUserRequest("名前", null, null, null);

        // Act
        var act = async () => await _sut.UpdateProfileAsync("no-user", request);

        // Assert
        var ex = await Should.ThrowAsync<NotFoundException>(act);
        ex.Message.ShouldContain("no-user");
    }

    #endregion

    #region UpdateStatusAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_UpdateStatus_When_UserExists()
    {
        // Arrange
        var user = CreateTestUser();
        _userRepository.FindByIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await _sut.UpdateStatusAsync("user-1", UserStatus.Suspended);

        // Assert
        user.Status.ShouldBe(UserStatus.Suspended);
        await _userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _cacheService.Received(1).RemoveAsync("user:profile:user-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowNotFoundException_When_UserNotFoundOnStatusUpdate()
    {
        // Arrange
        _userRepository.FindByIdAsync("no-user", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var act = async () => await _sut.UpdateStatusAsync("no-user", UserStatus.Active);

        // Assert
        var ex = await Should.ThrowAsync<NotFoundException>(act);
        ex.Message.ShouldContain("no-user");
    }

    #endregion

    #region SetProcessingRestrictionAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_SetRestriction_When_RestrictedIsTrue()
    {
        // Arrange
        var user = CreateTestUser();
        _userRepository.FindByIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await _sut.SetProcessingRestrictionAsync("user-1", true, "GDPR リクエスト");

        // Assert
        user.IsProcessingRestricted.ShouldBeTrue();
        user.RestrictionReason.ShouldBe("GDPR リクエスト");
        user.RestrictedAt.ShouldBe(FixedNow);
        await _userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ClearRestriction_When_RestrictedIsFalse()
    {
        // Arrange
        var user = CreateTestUser();
        user.IsProcessingRestricted = true;
        user.RestrictionReason = "GDPR リクエスト";
        user.RestrictedAt = FixedNow.AddDays(-5);
        _userRepository.FindByIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await _sut.SetProcessingRestrictionAsync("user-1", false, null);

        // Assert
        user.IsProcessingRestricted.ShouldBeFalse();
        user.RestrictionReason.ShouldBeNull();
        user.RestrictedAt.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowNotFoundException_When_UserNotFoundOnRestriction()
    {
        // Arrange
        _userRepository.FindByIdAsync("no-user", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var act = async () => await _sut.SetProcessingRestrictionAsync("no-user", true, "reason");

        // Assert
        var ex = await Should.ThrowAsync<NotFoundException>(act);
        ex.Message.ShouldContain("no-user");
    }

    #endregion

    #region GetAllAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnPaginatedUsers_When_Called()
    {
        // Arrange
        var users = new List<User>
        {
            CreateTestUser("user-1"),
            CreateTestUser("user-2")
        };
        _userRepository.FindAllAsync(1, 10, null, Arg.Any<CancellationToken>())
            .Returns((users, 2));

        // Act
        var (items, totalCount) = await _sut.GetAllAsync(1, 10, null);

        // Assert
        items.Count.ShouldBe(2);
        totalCount.ShouldBe(2);
        items[0].Id.ShouldBe("user-1");
        items[1].Id.ShouldBe("user-2");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnFilteredUsers_When_StatusFilterProvided()
    {
        // Arrange
        var users = new List<User> { CreateTestUser("user-1") };
        _userRepository.FindAllAsync(1, 20, UserStatus.Active, Arg.Any<CancellationToken>())
            .Returns((users, 1));

        // Act
        var (items, totalCount) = await _sut.GetAllAsync(1, 20, UserStatus.Active);

        // Assert
        items.Count.ShouldBe(1);
        totalCount.ShouldBe(1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnEmptyList_When_NoUsersFound()
    {
        // Arrange
        _userRepository.FindAllAsync(1, 10, null, Arg.Any<CancellationToken>())
            .Returns((new List<User>(), 0));

        // Act
        var (items, totalCount) = await _sut.GetAllAsync(1, 10, null);

        // Assert
        items.ShouldBeEmpty();
        totalCount.ShouldBe(0);
    }

    #endregion

    #region ResetLastLoginAtAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ResetLastLoginAt_When_UserExists()
    {
        // Arrange
        var user = CreateTestUser();
        user.LastLoginAt = FixedNow;
        _userRepository.FindByIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await _sut.ResetLastLoginAtAsync("user-1");

        // Assert
        user.LastLoginAt.ShouldBeNull();
        await _userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _cacheService.Received(1).RemoveAsync("user:profile:user-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowNotFoundException_When_UserNotFoundOnResetLogin()
    {
        // Arrange
        _userRepository.FindByIdAsync("no-user", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var act = async () => await _sut.ResetLastLoginAtAsync("no-user");

        // Assert
        var ex = await Should.ThrowAsync<NotFoundException>(act);
        ex.Message.ShouldContain("no-user");
    }

    #endregion

    #region MapToDto correctness

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_MapAllFieldsCorrectly_When_UserRetrieved()
    {
        // Arrange
        _cacheService.GetAsync<UserDto>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((UserDto?)null);
        var user = CreateTestUser();
        _userRepository.FindByIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        var result = await _sut.GetByIdAsync("user-1");

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(user.Id);
        result.Email.ShouldBe(user.Email);
        result.FirstName.ShouldBe(user.FirstName);
        result.LastName.ShouldBe(user.LastName);
        result.PhoneNumber.ShouldBe(user.PhoneNumber);
        result.BirthDate.ShouldBe(user.BirthDate);
        result.Status.ShouldBe(user.Status);
        result.IsProcessingRestricted.ShouldBe(user.IsProcessingRestricted);
        result.LastLoginAt.ShouldBe(user.LastLoginAt);
        result.CreatedAt.ShouldBe(user.CreatedAt);
        result.UpdatedAt.ShouldBe(user.UpdatedAt);
    }

    #endregion
}
