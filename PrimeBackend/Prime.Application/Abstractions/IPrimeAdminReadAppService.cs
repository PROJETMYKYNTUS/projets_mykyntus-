using Prime.Application.DTOs;
using Prime.Domain.Entities;

namespace Prime.Application.Abstractions;

public interface IPrimeAdminReadAppService
{
    AdminDashboardResponse GetAdminDashboard();
    Task<List<AdminAuditLog>> GetAuditLogsAsync(CancellationToken ct = default);
    AdminCalculationConfig GetCalculationConfig();
    AdminCalculationConfig SaveCalculationConfig(AdminCalculationConfig payload);
    List<AdminRbacRow> GetRbacMatrix();
    List<AdminRbacRow> ToggleRbacPermission(string role, string permission);
    AdminWorkflowConfig GetWorkflowConfig();
    AdminWorkflowConfig SaveWorkflowConfig(AdminWorkflowConfig payload);
    List<AdminAnomaly> GetAdminAnomalies();
    List<AdminAnomaly> UpdateAnomalyStatus(string id, string status);

    AuditDashboardResponse GetAuditDashboard();
    List<AuditOperation> GetOperations();
    List<AuditTrailLog> GetAuditTrailLogs();
    List<AuditAnomaly> GetAuditAnomalies();

    List<SupervisorPrimeRow> GetSupervisorPrimes(string supervisorUserId, string? period);
    SupervisorDashboardResponse GetSupervisorDashboard(string supervisorUserId);
    SupervisorPrimeRow ValidateAsSupervisor(string supervisorUserId, string resultId);
    SupervisorPrimeRow RejectAsSupervisor(string supervisorUserId, string resultId);
    SupervisorCalculateResponse ComputePrimeSupervisor(SupervisorCalculateRequest req);

    List<PrimeConfigItem> GetPrimeConfigs(string? kind, string? sector, string? groupCode, string? activityType);
    PrimeConfigItem CreatePrimeConfig(PrimeConfigUpsertRequest req);
    PrimeConfigItem UpdatePrimeConfig(string id, PrimeConfigUpsertRequest req);
    void DeletePrimeConfig(string id);
}
