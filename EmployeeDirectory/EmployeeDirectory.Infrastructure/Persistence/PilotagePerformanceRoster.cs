using EmployeeDirectory.Domain.Enums;

namespace EmployeeDirectory.Infrastructure.Persistence;

/// <summary>Roster pôle pilotage performance (miroir init/pilotage/roster.json).</summary>
internal static class PilotagePerformanceRoster
{
    internal static readonly Guid DepartmentId = Guid.Parse("33333333-3333-4333-8333-333333330001");
    internal const string DepartmentCode = "OP-002";
    internal const string DepartmentName = "OP-003 - département opérationnel";
    internal const string PoleId = "pole-pilotage-performance";
    internal const string PoleName = "pole pilotage performance";
    internal const string CelluleId = "cell-suivi-kpi";
    internal const string CelluleName = "cellule suivi KPI";
    internal const string ServiceId = "svc-analyse-operationnelle";
    internal const string ServiceName = "service analyse operationnelle";

    internal sealed record EmployeeSpec(
        Guid Id,
        string FirstName,
        string LastName,
        string Email,
        string Role,
        OrgAssignmentKind? Assignment,
        string Node);

    internal static readonly EmployeeSpec[] Employees =
    [
        new(Guid.Parse("33333333-3333-4333-8333-333333333001"), "Malak", "Souiri", "malak.souiri@contactcentre.ma", "Chef de projet", OrgAssignmentKind.ChefDeProjet, "pole"),
        new(Guid.Parse("33333333-3333-4333-8333-333333333002"), "Salim", "Ouazzani", "salim.ouazzani@contactcentre.ma", "Superviseur", OrgAssignmentKind.Superviseur, "cellule"),
        new(Guid.Parse("33333333-3333-4333-8333-333333333003"), "Younes", "Elidrissi", "younes.elidrissi@contactcentre.ma", "Référent technique", OrgAssignmentKind.ReferentTechnique, "service"),
        new(Guid.Parse("33333333-3333-4333-8333-333333333004"), "Chaima", "Benali", "chaima.benali@contactcentre.ma", "Pilote", OrgAssignmentKind.Pilote, "service"),
        new(Guid.Parse("33333333-3333-4333-8333-333333333005"), "Hamid", "Fellah", "hamid.fellah@contactcentre.ma", "Pilote", OrgAssignmentKind.Pilote, "service"),
        new(Guid.Parse("33333333-3333-4333-8333-333333333006"), "Othmane", "Kabbaj", "othmane.kabbaj@contactcentre.ma", "Pilote", OrgAssignmentKind.Pilote, "service"),
        new(Guid.Parse("33333333-3333-4333-8333-333333333007"), "Asmae", "Tazi", "asmae.tazi@contactcentre.ma", "Pilote", null, "service"),
        new(Guid.Parse("33333333-3333-4333-8333-333333333008"), "Rania", "Karimi", "rania.karimi@contactcentre.ma", "Pilote", null, "service"),
    ];
}
