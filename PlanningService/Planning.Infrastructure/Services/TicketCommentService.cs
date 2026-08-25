using Microsoft.EntityFrameworkCore;
using Planning.Application.Abstractions;
using Planning.Application.DTOs;
using Planning.Domain.Entities;
using Planning.Infrastructure.Persistence;

namespace Planning.Infrastructure.Services;

public class TicketCommentService : ITicketCommentService
{
    private readonly AppDbContext _db;
    private readonly IMediaAssetService _media;

    public TicketCommentService(AppDbContext db, IMediaAssetService media)
    {
        _db = db;
        _media = media;
    }

    public async Task<TicketCommentDto> AddAsync(
        MediaOwnerType ownerType,
        int ownerId,
        CreateTicketCommentDto dto,
        string authorId,
        string authorNom,
        CancellationToken ct = default)
    {
        if (ownerType is not (MediaOwnerType.Reclamation or MediaOwnerType.Proposition))
            throw new InvalidOperationException("Type de ticket invalide.");

        if (string.IsNullOrWhiteSpace(dto.Text))
            throw new InvalidOperationException("Le commentaire est obligatoire.");

        var exists = ownerType == MediaOwnerType.Reclamation
            ? await _db.Reclamations.AnyAsync(r => r.Id == ownerId, ct)
            : await _db.Propositions.AnyAsync(p => p.Id == ownerId, ct);
        if (!exists) throw new InvalidOperationException("Ticket introuvable.");

        var comment = new TicketComment
        {
            OwnerType = ownerType,
            OwnerId = ownerId,
            AuthorId = authorId,
            AuthorNom = authorNom,
            Text = dto.Text.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        _db.TicketComments.Add(comment);
        await _db.SaveChangesAsync(ct);

        if (dto.MediaIds is { Count: > 0 })
            await _media.AttachAsync(dto.MediaIds, MediaOwnerType.TicketComment, comment.Id, ct);

        var media = await _media.ListByOwnerAsync(MediaOwnerType.TicketComment, comment.Id, ct);
        var dtoResult = MediaDtoMapper.ToDto(comment);
        dtoResult.Media = media.ToList();
        return dtoResult;
    }

    public async Task<IReadOnlyList<TicketCommentDto>> ListAsync(
        MediaOwnerType ownerType,
        int ownerId,
        CancellationToken ct = default)
    {
        var comments = await _db.TicketComments
            .AsNoTracking()
            .Where(c => c.OwnerType == ownerType && c.OwnerId == ownerId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

        var result = new List<TicketCommentDto>();
        foreach (var c in comments)
        {
            var media = await _media.ListByOwnerAsync(MediaOwnerType.TicketComment, c.Id, ct);
            result.Add(new TicketCommentDto
            {
                Id = c.Id,
                OwnerType = c.OwnerType.ToString(),
                OwnerId = c.OwnerId,
                AuthorId = c.AuthorId,
                AuthorNom = c.AuthorNom,
                Text = c.Text,
                CreatedAt = c.CreatedAt,
                Media = media.ToList()
            });
        }
        return result;
    }
}
