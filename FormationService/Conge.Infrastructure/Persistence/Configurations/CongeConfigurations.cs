using Conge.Domain.Entities;
using Conge.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Conge.Infrastructure.Persistence.Configurations;

public class DemandeCongeConfiguration : IEntityTypeConfiguration<DemandeConge>
{
    public void Configure(EntityTypeBuilder<DemandeConge> builder)
    {
        builder.ToTable("demandes_conge");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EmployeId).IsRequired();
        builder.Property(x => x.ManagerId).IsRequired();

        builder.Property(x => x.TypeConge)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.TypeExceptionnel)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.Statut)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.NombreJours).IsRequired();
        builder.Property(x => x.DateDebut).IsRequired();
        builder.Property(x => x.DateFin).IsRequired();
        builder.Property(x => x.DateDemande).IsRequired();

        builder.Property(x => x.Motif).HasMaxLength(500);
        builder.Property(x => x.CommentaireManager).HasMaxLength(500);

        // Index pour les requêtes fréquentes
        builder.HasIndex(x => x.EmployeId);
        builder.HasIndex(x => x.ManagerId);
        builder.HasIndex(x => new { x.EmployeId, x.Statut });

        // Ignorer les domain events (pas persistés)
        builder.Ignore(x => x.DomainEvents);
    }
}

public class SoldeCongeConfiguration : IEntityTypeConfiguration<SoldeConge>
{
    public void Configure(EntityTypeBuilder<SoldeConge> builder)
    {
        builder.ToTable("soldes_conge");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.EmployeId).IsRequired();
        builder.Property(x => x.Annee).IsRequired();
        builder.Property(x => x.SoldeInitial).IsRequired();
        builder.Property(x => x.SoldeUtilise).IsRequired();

        // Unicité employé/année
        builder.HasIndex(x => new { x.EmployeId, x.Annee }).IsUnique();
    }
}

public class EmployeSnapshotConfiguration : IEntityTypeConfiguration<EmployeSnapshot>
{
    public void Configure(EntityTypeBuilder<EmployeSnapshot> builder)
    {
        builder.ToTable("employe_snapshots");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EmployeId).IsRequired();
        builder.Property(x => x.Nom).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Prenom).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ServiceNom).HasMaxLength(200);
        builder.Property(x => x.DateEmbauche).IsRequired();

        builder.HasIndex(x => x.EmployeId).IsUnique();
        builder.HasIndex(x => x.ManagerId);
    }
}