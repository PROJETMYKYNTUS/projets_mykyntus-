using Conge.Domain.Entities;
using Conge.Domain.Enums;
using Xunit;

namespace Conge.UnitTests;

public class PeriodeInterditeTests
{
    [Fact]
    public void Defaut_contient_septembre_et_octobre()
    {
        var cfg = PeriodeInterditeConge.CreerParDefaut();
        Assert.Equal(new[] { 9, 10 }, cfg.GetMois());
    }

    [Fact]
    public void Chevauche_mois_interdit_partiel()
    {
        var cfg = PeriodeInterditeConge.CreerParDefaut();
        Assert.True(cfg.ChevauchePeriode(new DateTime(2026, 8, 28), new DateTime(2026, 9, 2)));
        Assert.False(cfg.ChevauchePeriode(new DateTime(2026, 7, 1), new DateTime(2026, 7, 15)));
    }

    [Fact]
    public void MettreAJour_filtre_mois_invalides()
    {
        var cfg = PeriodeInterditeConge.CreerParDefaut();
        cfg.MettreAJour(new[] { 0, 3, 3, 13, 12 });
        Assert.Equal(new[] { 3, 12 }, cfg.GetMois());
    }
}

public class DualValidationTests
{
    private static (EmployeSnapshot emp, SoldeConge solde) Fixtures()
    {
        var emp = EmployeSnapshot.Creer(
            Guid.NewGuid(), "Doe", "Jane", "j@test.com",
            Guid.NewGuid(), Guid.NewGuid(), "Svc A",
            DateTime.UtcNow.AddYears(-2));
        var solde = SoldeConge.Initialiser(emp.EmployeId, 18, DateTime.Today.Year);
        return (emp, solde);
    }

    [Fact]
    public void Parcours_superviseur_puis_rh()
    {
        var (emp, solde) = Fixtures();
        var debut = DateTime.UtcNow.Date.AddDays(30);
        var fin = debut.AddDays(5);
        var d = DemandeConge.CreerCongeAnnuel(emp.EmployeId, emp.ManagerId, debut, fin, solde, emp);

        Assert.Equal(StatutDemande.EnAttente, d.Statut);
        d.ValiderParSuperviseur(emp.ManagerId, "ok sup");
        Assert.Equal(StatutDemande.EnAttenteRh, d.Statut);
        Assert.NotNull(d.DateValidationSuperviseur);

        d.ValiderParRh(Guid.NewGuid(), "ok rh");
        Assert.Equal(StatutDemande.Validee, d.Statut);
        Assert.NotNull(d.DateDecision);
    }

    [Fact]
    public void Refus_possible_depuis_en_attente_rh()
    {
        var (emp, solde) = Fixtures();
        var debut = DateTime.UtcNow.Date.AddDays(40);
        var fin = debut.AddDays(2);
        var d = DemandeConge.CreerCongeAnnuel(
            emp.EmployeId, emp.ManagerId, debut, fin, solde, emp,
            statutInitial: StatutDemande.EnAttenteRh);

        d.Refuser(Guid.NewGuid(), "Refus RH pour effectif");
        Assert.Equal(StatutDemande.Refusee, d.Statut);
    }

    [Fact]
    public void Annuler_autorise_en_attente_rh()
    {
        var (emp, solde) = Fixtures();
        var debut = DateTime.UtcNow.Date.AddDays(50);
        var fin = debut.AddDays(2);
        var d = DemandeConge.CreerCongeAnnuel(
            emp.EmployeId, emp.ManagerId, debut, fin, solde, emp,
            statutInitial: StatutDemande.EnAttenteRh);
        d.Annuler();
        Assert.Equal(StatutDemande.Annulee, d.Statut);
    }

    [Fact]
    public void Demande_superviseur_peut_partir_direct_en_attente_rh()
    {
        var (emp, solde) = Fixtures();
        var debut = DateTime.UtcNow.Date.AddDays(20);
        var fin = debut.AddDays(3);
        var d = DemandeConge.CreerCongeAnnuel(
            emp.EmployeId, emp.ManagerId, debut, fin, solde, emp,
            statutInitial: StatutDemande.EnAttenteRh);
        Assert.Equal(StatutDemande.EnAttenteRh, d.Statut);
    }
}
