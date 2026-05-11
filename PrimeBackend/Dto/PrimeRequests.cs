namespace PrimeBackend.Dto;

public class UpdatePrimeResultStatusRequest
{
    public string Status { get; set; } = "";
    public string? ApprovedBy { get; set; }
}

public class UpdateRpValidationStatusRequest
{
    public string Status { get; set; } = "";
}

public class UpdateAnomalyStatusRequest
{
    public string Status { get; set; } = "";
}

public class AssignManagerEtageRequest
{
    public string UserId { get; set; } = "";
    public string EtageId { get; set; } = "";
}

public class AssignSupervisorServiceRequest
{
    public string UserId { get; set; } = "";
    public string ServiceId { get; set; } = "";
}

public class AssignCoachSousServiceRequest
{
    public string UserId { get; set; } = "";
    public string SousServiceId { get; set; } = "";
}

public class AssignCoachPilotRequest
{
    public string CoachUserId { get; set; } = "";
    public string PilotUserId { get; set; } = "";
}

/// <summary>Corps pour désigner le responsable structurel (manager, superviseur, coach) sur un nœud d’org.</summary>
public class SetOrgResponsibleBody
{
    public string EmployeeId { get; set; } = "";
}

public class AddPilotToCelluleBody
{
    public string EmployeeId { get; set; } = "";
    /// <summary>Si null, première équipe de la cellule (règle stable).</summary>
    public string? TeamId { get; set; }
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

public class CreateOrgDepartmentBody
{
    public string Name { get; set; } = "";
}

public class CreateOrgNodeNameBody
{
    public string Name { get; set; } = "";
}
