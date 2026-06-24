using Microsoft.AspNetCore.Http;
using Parrainage.Application.DTOs;

namespace Parrainage.Application.Abstractions;

public interface IReferralAppService
{
    Task<IReadOnlyList<ReferralDto>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ReferralHistoryDto>> GetHistoryAsync(CancellationToken ct = default);
    Task<ReferralDto?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<ReferralRewardPreviewDto?> GetRewardPreviewAsync(string id, CancellationToken ct = default);
    Task<ReferralDto> CreateAsync(CreateReferralRequest body, CancellationToken ct = default);
    Task<ReferralDto?> UpdateAsync(string id, UpdateReferralRequest body, CancellationToken ct = default);
    Task<ReferralDto?> ProcessAsync(string id, ProcessReferralRequest body, CancellationToken ct = default);
    Task<ReferralDto?> ApproveAsync(string id, ApproveReferralRequest body, CancellationToken ct = default);
    Task<ReferralDto?> ConfirmProductionAsync(string id, ConfirmProductionStartRequest body, CancellationToken ct = default);
    Task<ReferralDto?> RejectEarlyDepartureAsync(string id, RejectEarlyDepartureRequest body, CancellationToken ct = default);
    Task<ReferralDto?> ExtendTrainingAsync(string id, ExtendTrainingRequest body, CancellationToken ct = default);
    Task<ReferralDto?> ConfirmEligibilityAsync(string id, ConfirmPaymentEligibilityRequest body, CancellationToken ct = default);
    Task<ReferralDto?> ChangeStatusAsync(string id, UpdateStatusRequest body, CancellationToken ct = default);
    Task<ReferralDto?> RewardAsync(string id, RewardRequest body, ParrainageResolvedUser user, CancellationToken ct = default);
    Task<ReferralDto?> MarkPaymentAsync(string id, MarkReferralPaymentRequest body, CancellationToken ct = default);
    Task<ReferralDto?> UploadCvAsync(string id, IFormFile file, CancellationToken ct = default);
    Task<ReferralCvFile?> OpenCvAsync(string id, CancellationToken ct = default);
}

public sealed record ReferralCvFile(Stream Stream, string ContentType, string FileName);
