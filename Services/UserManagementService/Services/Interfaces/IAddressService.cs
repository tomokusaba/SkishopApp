using UserManagementService.DTOs.Requests;
using UserManagementService.DTOs.Responses;

namespace UserManagementService.Services.Interfaces;

/// <summary>
/// 住所管理サービス。住所の CRUD およびデフォルト住所設定を提供する。
/// </summary>
public interface IAddressService
{
    Task<List<AddressDto>> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task<AddressDto> CreateAsync(string userId, CreateAddressRequest request, CancellationToken ct = default);
    Task<AddressDto> UpdateAsync(string userId, string addressId, UpdateAddressRequest request, CancellationToken ct = default);
    Task DeleteAsync(string userId, string addressId, CancellationToken ct = default);
}
