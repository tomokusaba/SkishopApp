using UserManagementService.DTOs.Requests;
using UserManagementService.DTOs.Responses;
using UserManagementService.Events;

namespace UserManagementService.Services.Interfaces;

/// <summary>
/// ユーザープロフィル管理サービス。プロフィル CRUD・ステータス管理・管理者操作・データエクスポートを提供する。
/// </summary>
public interface IUserService
{
    Task<UserDto?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<UserDto> UpdateProfileAsync(string id, UpdateUserRequest request, CancellationToken ct = default);
    Task InitializeRegisteredUserAsync(UserRegisteredEvent @event, CancellationToken ct = default);
    Task UpdateStatusAsync(string id, string status, CancellationToken ct = default);
    Task SetProcessingRestrictionAsync(string id, bool restricted, string? reason, CancellationToken ct = default);
    Task UpdateStatusByAdminAsync(string id, string status, string adminId, CancellationToken ct = default);
    Task UpdateProcessingRestrictionByAdminAsync(string id, bool restricted, string? reason, string adminId, CancellationToken ct = default);
    Task<(List<UserDto> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? statusFilter, CancellationToken ct = default);
    Task ResetLastLoginAtAsync(string userId, CancellationToken ct = default);
    Task RequestDataExportAsync(string userId, CancellationToken ct = default);
}
