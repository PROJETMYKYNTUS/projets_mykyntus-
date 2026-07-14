using EmployeeDirectory.Application.Dtos;
using EmployeeDirectory.Domain.Entities;
using EmployeeDirectory.Infrastructure.Persistence;
using Kyntus.Messaging.Contracts;
using Kyntus.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;

namespace EmployeeDirectory.Infrastructure.Services;

internal static class DirectoryHrProfileHelper
{
    internal static void ApplyManagers(
        Employee employee,
        Guid? chefDeProjetId,
        Guid? superviseurId,
        Guid? referentTechniqueId)
    {
        if (chefDeProjetId.HasValue)
            employee.ChefDeProjetId = chefDeProjetId;
        if (superviseurId.HasValue)
        {
            employee.SuperviseurId = superviseurId;
            employee.ParentId = superviseurId;
        }
        if (referentTechniqueId.HasValue)
            employee.ReferentTechniqueId = referentTechniqueId;
    }

    internal static async Task UpsertAsync(
        DirectoryDbContext db,
        IOutboxWriter outbox,
        Guid employeeId,
        EmployeeHrProfileDto? dto,
        DateTime? hireDateFallback,
        CancellationToken ct)
    {
        if (dto is null && hireDateFallback is null)
            return;

        var profile = await db.EmployeeHrProfiles.FirstOrDefaultAsync(p => p.EmployeeId == employeeId, ct);
        var isNew = profile is null;
        profile ??= new EmployeeHrProfile { EmployeeId = employeeId, CreatedAt = DateTimeOffset.UtcNow };

        if (dto is not null)
            ApplyDto(profile, dto);
        else if (hireDateFallback.HasValue && profile.DateEmbauche is null)
            profile.DateEmbauche = DateOnly.FromDateTime(hireDateFallback.Value);

        profile.UpdatedAt = DateTimeOffset.UtcNow;
        if (isNew)
            db.EmployeeHrProfiles.Add(profile);

        await EnqueueHrProfileChangedAsync(outbox, profile, isDeleted: false, ct);
    }

    internal static void ApplyDto(EmployeeHrProfile profile, EmployeeHrProfileDto dto)
    {
        profile.DateNaissance = dto.DateNaissance;
        profile.VilleNaissance = dto.VilleNaissance;
        profile.Nationalite = dto.Nationalite;
        profile.NumeroCarteAutoentrepreneur = dto.NumeroCarteAutoentrepreneur;
        profile.Sexe = dto.Sexe;
        profile.SituationFamiliale = dto.SituationFamiliale;
        profile.NombreEnfants = dto.NombreEnfants;
        profile.Cin = dto.Cin;
        profile.Adresse = dto.Adresse;
        profile.EmailPersonnel = dto.EmailPersonnel;
        profile.Telephone1 = dto.Telephone1;
        profile.TelephoneUrgence = dto.TelephoneUrgence;
        profile.RelationUrgence = dto.RelationUrgence;
        profile.Rib = dto.Rib;
        profile.ImmatriculationInterne = dto.ImmatriculationInterne;
        profile.ImmatriculationCnss = dto.ImmatriculationCnss;
        profile.DateEntree = dto.DateEntree;
        profile.DateEmbauche = dto.DateEmbauche;
        profile.DateAnciennete = dto.DateAnciennete;
        profile.DateSortie = dto.DateSortie;
        profile.DateEvolutionPoste = dto.DateEvolutionPoste;
        profile.AncienPoste = dto.AncienPoste;
        profile.AncienService = dto.AncienService;
        profile.NiveauScolaire = dto.NiveauScolaire;
        profile.IntitulesEtudes = dto.IntitulesEtudes;
        profile.EnFormation = dto.EnFormation;
        profile.DateDebutFormation = dto.DateDebutFormation;
        profile.DateFinFormationPrevue = dto.DateFinFormationPrevue;
        profile.NiveauExpertiseMetier = dto.NiveauExpertiseMetier;
    }

    internal static EmployeeHrProfileDto MapDto(EmployeeHrProfile p) => new(
        p.DateNaissance,
        p.VilleNaissance,
        p.Nationalite,
        p.NumeroCarteAutoentrepreneur,
        p.Sexe,
        p.SituationFamiliale,
        p.NombreEnfants,
        p.Cin,
        p.Adresse,
        p.EmailPersonnel,
        p.Telephone1,
        p.TelephoneUrgence,
        p.RelationUrgence,
        p.Rib,
        p.ImmatriculationInterne,
        p.ImmatriculationCnss,
        p.DateEntree,
        p.DateEmbauche,
        p.DateAnciennete,
        p.DateSortie,
        p.DateEvolutionPoste,
        p.AncienPoste,
        p.AncienService,
        p.NiveauScolaire,
        p.IntitulesEtudes,
        p.EnFormation,
        p.DateDebutFormation,
        p.DateFinFormationPrevue,
        p.NiveauExpertiseMetier);

    internal static async Task EnqueueHrProfileChangedAsync(
        IOutboxWriter outbox,
        EmployeeHrProfile profile,
        bool isDeleted,
        CancellationToken ct)
    {
        await outbox.EnqueueAsync(new DirectoryEmployeeHrProfileChangedMessage
        {
            EmployeeId = profile.EmployeeId,
            DateNaissance = profile.DateNaissance,
            VilleNaissance = profile.VilleNaissance,
            Nationalite = profile.Nationalite,
            NumeroCarteAutoentrepreneur = profile.NumeroCarteAutoentrepreneur,
            Sexe = profile.Sexe,
            SituationFamiliale = profile.SituationFamiliale,
            NombreEnfants = profile.NombreEnfants,
            Cin = profile.Cin,
            Adresse = profile.Adresse,
            EmailPersonnel = profile.EmailPersonnel,
            Telephone1 = profile.Telephone1,
            TelephoneUrgence = profile.TelephoneUrgence,
            RelationUrgence = profile.RelationUrgence,
            Rib = profile.Rib,
            ImmatriculationInterne = profile.ImmatriculationInterne,
            ImmatriculationCnss = profile.ImmatriculationCnss,
            DateEntree = profile.DateEntree,
            DateEmbauche = profile.DateEmbauche,
            DateAnciennete = profile.DateAnciennete,
            DateSortie = profile.DateSortie,
            DateEvolutionPoste = profile.DateEvolutionPoste,
            AncienPoste = profile.AncienPoste,
            AncienService = profile.AncienService,
            NiveauScolaire = profile.NiveauScolaire,
            IntitulesEtudes = profile.IntitulesEtudes,
            EnFormation = profile.EnFormation,
            DateDebutFormation = profile.DateDebutFormation,
            DateFinFormationPrevue = profile.DateFinFormationPrevue,
            NiveauExpertiseMetier = profile.NiveauExpertiseMetier,
            IsDeleted = isDeleted,
        }, aggregateId: profile.EmployeeId.ToString(), ct: ct);
    }
}
