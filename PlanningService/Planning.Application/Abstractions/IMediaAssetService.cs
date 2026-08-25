using Microsoft.AspNetCore.Http;
using Planning.Application.DTOs;
using Planning.Domain.Entities;

namespace Planning.Application.Abstractions;

public interface IMediaAssetService
{
    Task<MediaAssetDto> UploadAsync(IFormFile file, string userId, CancellationToken ct = default);
    Task AttachAsync(IEnumerable<int> mediaIds, MediaOwnerType ownerType, int ownerId, CancellationToken ct = default);
    Task<IReadOnlyList<MediaAssetDto>> ListByOwnerAsync(MediaOwnerType ownerType, int ownerId, CancellationToken ct = default);
    Task<(Stream Stream, string ContentType, string FileName)?> OpenReadAsync(int id, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, string userId, bool allowAdmin, CancellationToken ct = default);
}

public interface ITicketCommentService
{
    Task<TicketCommentDto> AddAsync(MediaOwnerType ownerType, int ownerId, CreateTicketCommentDto dto, string authorId, string authorNom, CancellationToken ct = default);
    Task<IReadOnlyList<TicketCommentDto>> ListAsync(MediaOwnerType ownerType, int ownerId, CancellationToken ct = default);
}
