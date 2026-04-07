using NSubstitute;
using Shouldly;
using Microsoft.Extensions.Logging;
using UserManagementService.DTOs.Requests;
using UserManagementService.DTOs.Responses;
using UserManagementService.Exceptions;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;
using UserManagementService.Services;

namespace UserManagementService.Tests.Unit.Services;

public class AddressServiceTests
{
    private readonly IAddressRepository _addressRepository;
    private readonly ILogger<AddressService> _logger;
    private readonly AddressService _sut;

    public AddressServiceTests()
    {
        _addressRepository = Substitute.For<IAddressRepository>();
        _logger = Substitute.For<ILogger<AddressService>>();
        _sut = new AddressService(_addressRepository, _logger);
    }

    private static Address CreateTestAddress(
        string id = "addr-1",
        string userId = "user-1",
        bool isDefault = false) => new()
    {
        Id = id,
        UserId = userId,
        AddressType = AddressType.Shipping,
        Recipient = "テスト太郎",
        ZipCode = "123-4567",
        Prefecture = "東京都",
        City = "渋谷区",
        StreetAddress = "道玄坂1-2-3",
        Building = "テストビル5F",
        PhoneNumber = "03-1234-5678",
        IsDefault = isDefault,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    #region GetByUserIdAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnAddressList_When_UserHasAddresses()
    {
        // Arrange
        var addresses = new List<Address>
        {
            CreateTestAddress("addr-1", "user-1", true),
            CreateTestAddress("addr-2", "user-1")
        };
        _addressRepository.FindByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(addresses);

        // Act
        var result = await _sut.GetByUserIdAsync("user-1");

        // Assert
        result.Count.ShouldBe(2);
        result[0].Id.ShouldBe("addr-1");
        result[0].IsDefault.ShouldBeTrue();
        result[1].Id.ShouldBe("addr-2");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnEmptyList_When_UserHasNoAddresses()
    {
        // Arrange
        _addressRepository.FindByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(new List<Address>());

        // Act
        var result = await _sut.GetByUserIdAsync("user-1");

        // Assert
        result.ShouldBeEmpty();
    }

    #endregion

    #region CreateAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_CreateAddress_When_UnderLimit()
    {
        // Arrange
        _addressRepository.CountByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(3);
        var request = new CreateAddressRequest(
            AddressType.Shipping, "新しい太郎", "100-0001", "東京都",
            "千代田区", "丸の内1-1-1", "テストビル", "03-9999-9999");

        // Act
        var result = await _sut.CreateAsync("user-1", request);

        // Assert
        result.ShouldNotBeNull();
        result.Recipient.ShouldBe("新しい太郎");
        result.ZipCode.ShouldBe("100-0001");
        result.IsDefault.ShouldBeFalse();
        await _addressRepository.Received(1).AddAsync(
            Arg.Is<Address>(a => a.UserId == "user-1" && a.Recipient == "新しい太郎"),
            Arg.Any<CancellationToken>());
        await _addressRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_SetFirstAddressAsDefault_When_NoExistingAddresses()
    {
        // Arrange
        _addressRepository.CountByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(0);
        var request = new CreateAddressRequest(
            AddressType.Shipping, "初回太郎", "100-0001", "東京都",
            "千代田区", "丸の内1-1-1", null, null);

        // Act
        var result = await _sut.CreateAsync("user-1", request);

        // Assert
        result.IsDefault.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowBusinessException_When_AddressLimitReached()
    {
        // Arrange
        _addressRepository.CountByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(10);
        var request = new CreateAddressRequest(
            AddressType.Shipping, "超過太郎", "100-0001", "東京都",
            "千代田区", "丸の内1-1-1", null, null);

        // Act
        var act = async () => await _sut.CreateAsync("user-1", request);

        // Assert
        var ex = await Should.ThrowAsync<BusinessException>(act);
        ex.Message.ShouldContain("10");
    }

    #endregion

    #region UpdateAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_UpdateAddress_When_OwnerMatches()
    {
        // Arrange
        var address = CreateTestAddress();
        _addressRepository.FindByIdAsync("addr-1", Arg.Any<CancellationToken>())
            .Returns(address);
        var request = new UpdateAddressRequest(
            "更新太郎", "200-0001", null, null, null, null, null, null);

        // Act
        var result = await _sut.UpdateAsync("user-1", "addr-1", request);

        // Assert
        result.Recipient.ShouldBe("更新太郎");
        result.ZipCode.ShouldBe("200-0001");
        result.Prefecture.ShouldBe("東京都");
        await _addressRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_UpdateOnlyProvidedFields_When_PartialUpdate()
    {
        // Arrange
        var address = CreateTestAddress();
        _addressRepository.FindByIdAsync("addr-1", Arg.Any<CancellationToken>())
            .Returns(address);
        var request = new UpdateAddressRequest(
            null, null, null, "新宿区", null, null, null, true);

        // Act
        var result = await _sut.UpdateAsync("user-1", "addr-1", request);

        // Assert
        result.City.ShouldBe("新宿区");
        result.IsDefault.ShouldBeTrue();
        result.Recipient.ShouldBe("テスト太郎");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowNotFoundException_When_AddressNotFound()
    {
        // Arrange
        _addressRepository.FindByIdAsync("no-addr", Arg.Any<CancellationToken>())
            .Returns((Address?)null);
        var request = new UpdateAddressRequest(
            "名前", null, null, null, null, null, null, null);

        // Act
        var act = async () => await _sut.UpdateAsync("user-1", "no-addr", request);

        // Assert
        var ex = await Should.ThrowAsync<NotFoundException>(act);
        ex.Message.ShouldContain("no-addr");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowForbiddenException_When_UserDoesNotOwnAddress()
    {
        // Arrange
        var address = CreateTestAddress(userId: "other-user");
        _addressRepository.FindByIdAsync("addr-1", Arg.Any<CancellationToken>())
            .Returns(address);
        var request = new UpdateAddressRequest(
            "名前", null, null, null, null, null, null, null);

        // Act
        var act = async () => await _sut.UpdateAsync("user-1", "addr-1", request);

        // Assert
        await Should.ThrowAsync<ForbiddenException>(act);
    }

    #endregion

    #region DeleteAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_DeleteAddress_When_OwnerMatches()
    {
        // Arrange
        var address = CreateTestAddress();
        _addressRepository.FindByIdAsync("addr-1", Arg.Any<CancellationToken>())
            .Returns(address);

        // Act
        await _sut.DeleteAsync("user-1", "addr-1");

        // Assert
        _addressRepository.Received(1).Remove(address);
        await _addressRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowNotFoundException_When_AddressNotFoundOnDelete()
    {
        // Arrange
        _addressRepository.FindByIdAsync("no-addr", Arg.Any<CancellationToken>())
            .Returns((Address?)null);

        // Act
        var act = async () => await _sut.DeleteAsync("user-1", "no-addr");

        // Assert
        var ex = await Should.ThrowAsync<NotFoundException>(act);
        ex.Message.ShouldContain("no-addr");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowForbiddenException_When_UserDoesNotOwnAddressOnDelete()
    {
        // Arrange
        var address = CreateTestAddress(userId: "other-user");
        _addressRepository.FindByIdAsync("addr-1", Arg.Any<CancellationToken>())
            .Returns(address);

        // Act
        var act = async () => await _sut.DeleteAsync("user-1", "addr-1");

        // Assert
        await Should.ThrowAsync<ForbiddenException>(act);
    }

    #endregion

    #region MapToDto correctness

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_MapAllFieldsCorrectly_When_AddressRetrieved()
    {
        // Arrange
        var address = CreateTestAddress(isDefault: true);
        _addressRepository.FindByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(new List<Address> { address });

        // Act
        var result = await _sut.GetByUserIdAsync("user-1");

        // Assert
        var dto = result.First();
        dto.Id.ShouldBe(address.Id);
        dto.UserId.ShouldBe(address.UserId);
        dto.AddressType.ShouldBe(AddressType.Shipping);
        dto.Recipient.ShouldBe("テスト太郎");
        dto.ZipCode.ShouldBe("123-4567");
        dto.Prefecture.ShouldBe("東京都");
        dto.City.ShouldBe("渋谷区");
        dto.StreetAddress.ShouldBe("道玄坂1-2-3");
        dto.Building.ShouldBe("テストビル5F");
        dto.PhoneNumber.ShouldBe("03-1234-5678");
        dto.IsDefault.ShouldBeTrue();
    }

    #endregion
}
