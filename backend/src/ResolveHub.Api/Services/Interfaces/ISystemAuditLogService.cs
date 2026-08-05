using ResolveHub.Api.DTOs.Admin;

namespace ResolveHub.Api.Services.Interfaces;

public interface ISystemAuditLogService
{
    Task<SystemAuditPageDto> GetAsync(SystemAuditFilterDto filter, CancellationToken token);
}
