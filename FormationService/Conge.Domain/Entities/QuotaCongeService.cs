namespace Conge.Domain.Entities;

/// <summary>
/// Quota d'absences simultanées (employés absents le même jour) pour un service.
/// </summary>
public class QuotaCongeService
{
    public Guid Id { get; private set; }
    public Guid ServiceId { get; private set; }
    public int MaxAbsentsSimultanes { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    private QuotaCongeService() { }

    public static QuotaCongeService Creer(Guid serviceId, int maxAbsents, Guid? updatedBy = null)
    {
        if (serviceId == Guid.Empty)
            throw new ArgumentException("ServiceId requis.", nameof(serviceId));
        if (maxAbsents < 1)
            throw new ArgumentException("Le quota doit être au moins 1.", nameof(maxAbsents));

        return new QuotaCongeService
        {
            Id = Guid.NewGuid(),
            ServiceId = serviceId,
            MaxAbsentsSimultanes = maxAbsents,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = updatedBy
        };
    }

    public void MettreAJour(int maxAbsents, Guid? updatedBy = null)
    {
        if (maxAbsents < 1)
            throw new ArgumentException("Le quota doit être au moins 1.", nameof(maxAbsents));
        MaxAbsentsSimultanes = maxAbsents;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }
}
