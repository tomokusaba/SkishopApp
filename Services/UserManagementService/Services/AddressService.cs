using UserManagementService.DTOs.Requests;
using UserManagementService.DTOs.Responses;
using UserManagementService.Exceptions;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.Services;

/// <summary>
/// 住所管理のビジネスロジック。ユーザーあたり最大 10 件の制限と IDOR 防止を担当する。
/// </summary>
public class AddressService(
    IAddressRepository addressRepository,
    ILogger<AddressService> logger) : IAddressService
{
    /// <summary>ユーザーあたりの住所保持上限。</summary>
    private const int MaxAddressesPerUser = 10;

    public async Task<List<AddressDto>> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        var addresses = await addressRepository.FindByUserIdAsync(userId, ct);
        return addresses.Select(MapToDto).ToList();
    }

    public async Task<AddressDto> CreateAsync(
        string userId, CreateAddressRequest request, CancellationToken ct = default)
    {
        var count = await addressRepository.CountByUserIdAsync(userId, ct);
        if (count >= MaxAddressesPerUser)
            throw new BusinessException($"住所は最大 {MaxAddressesPerUser} 件までです");

        var address = new Address
        {
            UserId = userId,
            AddressType = request.AddressType,
            Recipient = request.Recipient,
            ZipCode = request.ZipCode,
            Prefecture = request.Prefecture,
            City = request.City,
            StreetAddress = request.StreetAddress,
            Building = request.Building,
            PhoneNumber = request.PhoneNumber,
            IsDefault = count == 0
        };

        await addressRepository.AddAsync(address, ct);
        await addressRepository.SaveChangesAsync(ct);
        logger.LogInformation("住所が作成されました: {AddressId}, {UserId}", address.Id, userId);
        return MapToDto(address);
    }

    public async Task<AddressDto> UpdateAsync(
        string userId, string addressId, UpdateAddressRequest request, CancellationToken ct = default)
    {
        var address = await addressRepository.FindByIdAsync(addressId, ct)
            ?? throw new NotFoundException($"住所が見つかりません (ID: {addressId})");

        if (address.UserId != userId)
            throw new ForbiddenException("この住所にアクセスする権限がありません");

        if (request.Recipient is not null) address.Recipient = request.Recipient;
        if (request.ZipCode is not null) address.ZipCode = request.ZipCode;
        if (request.Prefecture is not null) address.Prefecture = request.Prefecture;
        if (request.City is not null) address.City = request.City;
        if (request.StreetAddress is not null) address.StreetAddress = request.StreetAddress;
        if (request.Building is not null) address.Building = request.Building;
        if (request.PhoneNumber is not null) address.PhoneNumber = request.PhoneNumber;
        if (request.IsDefault is true) address.SetAsDefault();

        await addressRepository.SaveChangesAsync(ct);
        logger.LogInformation("住所が更新されました: {AddressId}", addressId);
        return MapToDto(address);
    }

    public async Task DeleteAsync(string userId, string addressId, CancellationToken ct = default)
    {
        var address = await addressRepository.FindByIdAsync(addressId, ct)
            ?? throw new NotFoundException($"住所が見つかりません (ID: {addressId})");

        if (address.UserId != userId)
            throw new ForbiddenException("この住所にアクセスする権限がありません");

        addressRepository.Remove(address);
        await addressRepository.SaveChangesAsync(ct);
        logger.LogInformation("住所が削除されました: {AddressId}", addressId);
    }

    private static AddressDto MapToDto(Address a) =>
        new(a.Id, a.UserId, a.AddressType, a.Recipient, a.ZipCode,
            a.Prefecture, a.City, a.StreetAddress, a.Building,
            a.PhoneNumber, a.IsDefault, a.CreatedAt);
}
