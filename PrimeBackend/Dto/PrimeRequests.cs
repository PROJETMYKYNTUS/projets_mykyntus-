using System.Text.Json.Serialization;

namespace PrimeBackend.Dto;

public class UpdatePrimeResultStatusRequest
{
    public string Status { get; set; } = "";
    public string? ApprovedBy { get; set; }
}

public class UpdateChefProjetValidationStatusRequest
{
    public string Status { get; set; } = "";
}

public class UpdateAnomalyStatusRequest
{
    public string Status { get; set; } = "";
}

public class AssignChefProjetPoleRequest
{
    public string UserId { get; set; } = "";
    public string PoleId { get; set; } = "";
}

public class AssignSupervisorCelluleRequest
{
    public string UserId { get; set; } = "";
    public string CelluleId { get; set; } = "";

    // ---- LEGACY COMPAT (Phase 0 - to remove in Phase 1.6) ----
    public string ServiceId { get => CelluleId; set => CelluleId = value; }
}

public class AssignReferentTechniqueServiceRequest
{
    public string UserId { get; set; } = "";
    public string ServiceId { get; set; } = "";
}

public class AssignReferentTechniquePilotRequest
{
    [JsonPropertyName("coachUserId")]
    public string ReferentTechniqueUserId { get; set; } = "";
    public string PilotUserId { get; set; } = "";
}

/// <summary>Corps pour désigner le responsable structurel (chef de projet, superviseur, référent technique) sur un nœud d'org.</summary>
public class SetOrgResponsibleBody
{
    public string EmployeeId { get; set; } = "";
}

public class AddPilotToServiceBody
{
    public string EmployeeId { get; set; } = "";
    /// <summary>Identifiant d’équipe (JSON <c>teamId</c> depuis le front Angular).</summary>
    [JsonPropertyName("teamId")]
    public string? TeamId { get; set; }
    /// <summary>Alias historique : équipe ou repli.</summary>
    public string? ServiceId { get; set; }
}

public class SupervisorValidateRequest
{
    public string SupervisorUserId { get; set; } = "";
    public string ResultId { get; set; } = "";
    public string? Comment { get; set; }
}

public class SupervisorRejectRequest
{
    public string SupervisorUserId { get; set; } = "";
    public string ResultId { get; set; } = "";
    public string Reason { get; set; } = "";
}

public class SupervisorCalculateRequest
{
    public string SupervisorUserId { get; set; } = "";
    public string ResultId { get; set; } = "";
    public decimal BaseAmount { get; set; }
    public decimal Score { get; set; }
    public decimal Coefficient { get; set; }
    public decimal Penalty { get; set; }
    public decimal Bonus { get; set; }
}

public class SupervisorPrimeRow
{
    public string Id { get; set; } = "";
    public string EmployeeId { get; set; } = "";
    public string EmployeeName { get; set; } = "";
    public string Status { get; set; } = "";
    public int Amount { get; set; }
    public int Score { get; set; }
    public string Period { get; set; } = "";
}

public class SupervisorDashboardResponse
{
    public int Pending { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public int Anomalies { get; set; }
}

public class SupervisorCalculateResponse
{
    public int GlobalScore { get; set; }
    public int FinalAmount { get; set; }
    public int PenaltyApplied { get; set; }
    public int BonusApplied { get; set; }
}
public class PrimeConfigUpsertRequest
{
    public string Kind { get; set; } = "";
    public string Sector { get; set; } = "";
    public string GroupCode { get; set; } = "";
    public string ActivityType { get; set; } = "";
    public string? Label { get; set; }
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
    public bool InvertedLogic { get; set; }
    public decimal? Weight { get; set; }
    public decimal? PrimeCap { get; set; }
    public decimal? ChallengeCap { get; set; }
}

public class ToggleRbacPermissionRequest
{
    public string Role { get; set; } = "";
    public string Permission { get; set; } = ""; // read|edit|validate|configure
}

public class CreateOrgPoleBody
{
    public string Name { get; set; } = "";
}

public class CreateOrgNodeNameBody
{
    public string Name { get; set; } = "";
}
