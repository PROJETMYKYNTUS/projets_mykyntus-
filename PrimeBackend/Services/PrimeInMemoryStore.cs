namespace PrimeBackend.Services;

using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Models;
using PrimeBackend.Dto;

public class PrimeInMemoryStore
{
    private readonly List<Department> _departments;
    private readonly List<Employee> _employees;
    private readonly List<PrimeType> _primeTypes;
    private readonly List<PrimeRule> _primeRules;
    private readonly List<PrimeResult> _primeResults;
    private readonly List<PrimeConfigItem> _primeConfigs;
    private readonly List<EtageNode> _etages = [];
    private readonly List<ServiceNode> _services = [];
    private readonly List<SousServiceNode> _sousServices = [];
    private readonly List<ManagerEtageAssignment> _managerEtageAssignments;
    private readonly List<SupervisorServiceAssignment> _supervisorServiceAssignments;
    private readonly List<CoachSousServiceAssignment> _coachSousServiceAssignments;
    private readonly List<CoachPilotLink> _coachPilotLinks;

    private readonly Dictionary<string, List<string>> _rpProjectAssignments;
    private readonly List<RpTeamMemberPerformance> _rpTeamPerformance;
    private readonly List<RpValidationItem> _rpValidationItems;

    private readonly AdminCalculationConfig _adminCalculationConfig;
    private readonly List<AdminRbacRow> _adminRbacMatrix;
    private readonly AdminWorkflowConfig _adminWorkflow;
    private readonly List<AdminAuditLog> _adminAuditLogs;
    private readonly List<AdminAnomaly> _adminAnomalies;
    private readonly AdminDashboardCharts _adminCharts;
    private readonly List<AdminSystemAlert> _adminAlerts;
    private readonly AdminSystemKpi _adminSystemKpis;

    private readonly AuditKpis _auditKpis;
    private readonly AuditDashboardCharts _auditCharts;
    private readonly List<AuditOperation> _auditOperations;
    private readonly List<AuditTrailLog> _auditTrailLogs;
    private readonly List<AuditAnomaly> _auditAnomalies;

    public PrimeInMemoryStore()
    {
        // PrimeService mock-data (from TS)
        _departments =
        [
            new Department
            {
                Id = "d1",
                Name = "Operations",
                Poles =
                [
                    new Pole
                    {
                        Id = "p1",
                        Name = "Pôle Client",
                        DepartmentId = "d1",
                        Cells =
                        [
                            new Cellule
                            {
                                Id = "c1",
                                Name = "Support Client",
                                PoleId = "p1",
                                Teams =
                                [
                                    new Team { Id = "t1", Name = "Team Alpha", CelluleId = "c1" },
                                    new Team { Id = "t2", Name = "Team Beta", CelluleId = "c1" },
                                ]
                            },
                            new Cellule
                            {
                                Id = "c2",
                                Name = "Satisfaction Client",
                                PoleId = "p1",
                                Teams =
                                [
                                    new Team { Id = "t3", Name = "Team Gamma", CelluleId = "c2" },
                                ]
                            },
                        ]
                    },
                    new Pole
                    {
                        Id = "p2",
                        Name = "Pôle Escalade",
                        DepartmentId = "d1",
                        Cells =
                        [
                            new Cellule
                            {
                                Id = "c3",
                                Name = "Gestion de retards",
                                PoleId = "p2",
                                Teams =
                                [
                                    new Team { Id = "t4", Name = "Team Delta", CelluleId = "c3" },
                                ]
                            }
                        ]
                    }
                ]
            },
            new Department
            {
                Id = "d2",
                Name = "IT / Technical",
                Poles =
                [
                    new Pole
                    {
                        Id = "p3",
                        Name = "Infrastructure",
                        DepartmentId = "d2",
                        Cells =
                        [
                            new Cellule
                            {
                                Id = "c4",
                                Name = "Network",
                                PoleId = "p3",
                                Teams =
                                [
                                    new Team { Id = "t5", Name = "NetOps", CelluleId = "c4" },
                                ]
                            }
                        ]
                    }
                ]
            }
        ];

        _employees =
        [
            new Employee { Id = "e1", FirstName = "Alice", LastName = "Dupont", Role = "Pilote", ParentId = "e8", TeamId = "t1", DepartementId = "d1", PoleId = "p1", CelluleId = "c1", Email = "alice.dupont@mykyntus.com" },
            new Employee { Id = "e2", FirstName = "Bob", LastName = "Martin", Role = "Pilote", ParentId = "e8", TeamId = "t1", DepartementId = "d1", PoleId = "p1", CelluleId = "c1", Email = "bob.martin@mykyntus.com" },
            new Employee { Id = "e3", FirstName = "Charlie", LastName = "Durand", Role = "Manager", ParentId = "e6", TeamId = "t1", DepartementId = "d1", PoleId = "p1", CelluleId = "c1", Email = "charlie.durand@mykyntus.com" },
            new Employee { Id = "e4", FirstName = "Diana", LastName = "Bernard", Role = "Pilote", ParentId = "e8", TeamId = "t2", DepartementId = "d1", PoleId = "p1", CelluleId = "c1", Email = "diana.bernard@mykyntus.com" },
            new Employee { Id = "e5", FirstName = "Eve", LastName = "Thomas", Role = "RH", TeamId = "t5", DepartementId = "d2", PoleId = "p3", CelluleId = "c4", Email = "eve.thomas@mykyntus.com" },
            new Employee { Id = "e6", FirstName = "Rachid", LastName = "El Amrani", Role = "RP", TeamId = "t1", DepartementId = "d1", PoleId = "p1", CelluleId = "c1", Email = "rachid.elamrani@mykyntus.com" },
            new Employee { Id = "e7", FirstName = "Salma", LastName = "Bennani", Role = "Audit", TeamId = "t1", DepartementId = "d1", PoleId = "p1", CelluleId = "c1", Email = "salma.bennani@mykyntus.com" },
            new Employee { Id = "e8", FirstName = "Marc", LastName = "Lefèvre", Role = "Coach", ParentId = "e9", TeamId = "t1", DepartementId = "d1", PoleId = "p1", CelluleId = "c1", Email = "marc.lefevre@mykyntus.com" },
            new Employee { Id = "e9", FirstName = "Nadia", LastName = "Karimi", Role = "Superviseur", ParentId = "e3", TeamId = "t1", DepartementId = "d1", PoleId = "p1", CelluleId = "c1", Email = "nadia.karimi@mykyntus.com" },
            new Employee { Id = "e-admin", FirstName = "Système", LastName = "Admin", Role = "Admin", TeamId = "t1", DepartementId = "d1", PoleId = "p1", CelluleId = "c1", Email = "admin@mykyntus.com" }
        ];

        _primeTypes =
        [
            new PrimeType { Id = "pt1", Name = "Performance Bonus", Type = "Performance", DepartmentId = "d1", Status = "Active", Description = "Monthly performance bonus for operations" },
            new PrimeType { Id = "pt2", Name = "Zero Error Bonus", Type = "Quality", DepartmentId = "d1", Status = "Active", Description = "Bonus for 0 errors in a month" },
            new PrimeType { Id = "pt3", Name = "Overtime Bonus", Type = "Attendance", DepartmentId = "d2", Status = "Inactive", Description = "Bonus for extra hours" },
            new PrimeType { Id = "pt4", Name = "Escalation Resolution Bonus", Type = "Performance", DepartmentId = "d1", Status = "Active", Description = "Bonus for resolving escalations quickly" }
        ];

        _primeRules =
        [
            new PrimeRule
            {
                Id = "pr1",
                PrimeTypeId = "pt1",
                DepartmentId = "d1",
                ConditionField = "tickets_resolved",
                ConditionType = ">",
                TargetValue = 100,
                CalculationMethod = "Fixed",
                Amount = 300,
                Period = "Monthly"
            },
            new PrimeRule
            {
                Id = "pr2",
                PrimeTypeId = "pt2",
                DepartmentId = "d1",
                ConditionField = "errors",
                ConditionType = "==",
                TargetValue = 0,
                CalculationMethod = "Fixed",
                Amount = 500,
                Period = "Monthly"
            }
        ];

        _primeResults =
        [
            new PrimeResult { Id = "res1", EmployeeId = "e1", PrimeTypeId = "pt1", Score = 120, Amount = 300, Status = "Pending", Period = "2026-03", Date = "2026-03-15" },
            new PrimeResult { Id = "res2", EmployeeId = "e2", PrimeTypeId = "pt1", Score = 95, Amount = 0, Status = "Rejected", Period = "2026-03", Date = "2026-03-15" },
            new PrimeResult { Id = "res3", EmployeeId = "e4", PrimeTypeId = "pt2", Score = 0, Amount = 500, Status = "Coach Approved", Period = "2026-03", Date = "2026-03-14" },
            new PrimeResult { Id = "res7", EmployeeId = "e4", PrimeTypeId = "pt1", Score = 92, Amount = 350, Status = "Superviseur Approved", Period = "2026-03", Date = "2026-03-16" },
            new PrimeResult { Id = "res4", EmployeeId = "e1", PrimeTypeId = "pt2", Score = 0, Amount = 500, Status = "RH Approved", Period = "2026-02", Date = "2026-02-28", ApprovedBy = "e5" },
            new PrimeResult { Id = "res5", EmployeeId = "e2", PrimeTypeId = "pt4", Score = 15, Amount = 450, Status = "RP Approved", Period = "2026-03", Date = "2026-03-10" },
            new PrimeResult { Id = "res6", EmployeeId = "e1", PrimeTypeId = "pt1", Score = 88, Amount = 200, Status = "Manager Approved", Period = "2026-03", Date = "2026-03-12" }
        ];

        _primeConfigs =
        [
            new PrimeConfigItem { Id = "cfg-kpi-1", Kind = "KpiDefinition", Sector = "RACC", GroupCode = "A", ActivityType = "PLP", Label = "TauxReport" },
            new PrimeConfigItem { Id = "cfg-thr-1", Kind = "KpiThreshold", Sector = "RACC", GroupCode = "A", ActivityType = "PLP", Label = "TauxReport", Min = 7m, Max = 13m, InvertedLogic = true },
            new PrimeConfigItem { Id = "cfg-wgt-1", Kind = "KpiWeight", Sector = "RACC", GroupCode = "A", ActivityType = "PLP", Label = "TauxReport", Weight = 0.05m },
            new PrimeConfigItem { Id = "cfg-chl-1", Kind = "ChallengeConfig", Sector = "RACC", GroupCode = "A", ActivityType = "PLP", Label = "SatcliOk", Min = 0m, Max = 0.90m, Weight = 0.02m },
            new PrimeConfigItem { Id = "cfg-cap-1", Kind = "PrimeCapSettings", Sector = "RACC", GroupCode = "A", ActivityType = "PLP", PrimeCap = 1000m, ChallengeCap = 500m }
        ];

        RebuildOrgDerivedLists();

        _managerEtageAssignments =
        [
            new ManagerEtageAssignment { Id = "mea-1", UserId = "e3", EtageId = "d1" }
        ];
        _supervisorServiceAssignments =
        [
            new SupervisorServiceAssignment { Id = "ssa-1", UserId = "e9", ServiceId = "p1" }
        ];
        _coachSousServiceAssignments =
        [
            new CoachSousServiceAssignment { Id = "csa-1", UserId = "e8", SousServiceId = "c1" }
        ];
        _coachPilotLinks =
        [
            new CoachPilotLink { Id = "cpl-1", CoachUserId = "e8", PilotUserId = "e1" },
            new CoachPilotLink { Id = "cpl-2", CoachUserId = "e8", PilotUserId = "e2" },
            new CoachPilotLink { Id = "cpl-3", CoachUserId = "e8", PilotUserId = "e4" }
        ];

        // RP mock-data
        _rpProjectAssignments = new Dictionary<string, List<string>>
        {
            ["e6"] = ["proj-alpha"]
        };

        _rpTeamPerformance =
        [
            new RpTeamMemberPerformance
            {
                EmployeeId = "e1",
                EmployeeName = "Alice Dupont",
                ProjectId = "proj-alpha",
                ProjectName = "Projet Alpha",
                CompletedTasks = 26,
                TotalTasks = 30,
                ObjectivesReached = 4,
                TotalObjectives = 5,
                MonthlyPerformance =
                [
                    new MonthlyPerformancePoint { Month = "Oct", Score = 78 },
                    new MonthlyPerformancePoint { Month = "Nov", Score = 81 },
                    new MonthlyPerformancePoint { Month = "Dec", Score = 84 },
                    new MonthlyPerformancePoint { Month = "Jan", Score = 86 },
                    new MonthlyPerformancePoint { Month = "Feb", Score = 88 },
                    new MonthlyPerformancePoint { Month = "Mar", Score = 91 }
                ]
            },
            new RpTeamMemberPerformance
            {
                EmployeeId = "e2",
                EmployeeName = "Bob Martin",
                ProjectId = "proj-alpha",
                ProjectName = "Projet Alpha",
                CompletedTasks = 20,
                TotalTasks = 28,
                ObjectivesReached = 3,
                TotalObjectives = 5,
                MonthlyPerformance =
                [
                    new MonthlyPerformancePoint { Month = "Oct", Score = 70 },
                    new MonthlyPerformancePoint { Month = "Nov", Score = 72 },
                    new MonthlyPerformancePoint { Month = "Dec", Score = 74 },
                    new MonthlyPerformancePoint { Month = "Jan", Score = 76 },
                    new MonthlyPerformancePoint { Month = "Feb", Score = 78 },
                    new MonthlyPerformancePoint { Month = "Mar", Score = 80 }
                ]
            },
            new RpTeamMemberPerformance
            {
                EmployeeId = "e4",
                EmployeeName = "Diana Bernard",
                ProjectId = "proj-alpha",
                ProjectName = "Projet Alpha",
                CompletedTasks = 28,
                TotalTasks = 30,
                ObjectivesReached = 5,
                TotalObjectives = 5,
                MonthlyPerformance =
                [
                    new MonthlyPerformancePoint { Month = "Oct", Score = 82 },
                    new MonthlyPerformancePoint { Month = "Nov", Score = 85 },
                    new MonthlyPerformancePoint { Month = "Dec", Score = 87 },
                    new MonthlyPerformancePoint { Month = "Jan", Score = 89 },
                    new MonthlyPerformancePoint { Month = "Feb", Score = 92 },
                    new MonthlyPerformancePoint { Month = "Mar", Score = 94 }
                ]
            }
        ];

        _rpValidationItems =
        [
            new RpValidationItem { Id = "rpv1", EmployeeId = "e1", EmployeeName = "Alice Dupont", ProjectId = "proj-alpha", ProjectName = "Projet Alpha", PerformanceScore = 91, ManagerValidated = true, Status = "Manager Approved", Period = "2026-03" },
            new RpValidationItem { Id = "rpv2", EmployeeId = "e2", EmployeeName = "Bob Martin", ProjectId = "proj-alpha", ProjectName = "Projet Alpha", PerformanceScore = 80, ManagerValidated = true, Status = "Manager Approved", Period = "2026-03" }
        ];

        // Admin mock-data
        _adminSystemKpis = new AdminSystemKpi
        {
            TotalGeneratedPrimes = 1486,
            ValidationsInProgress = 73,
            ErrorCount = 12,
            AvgProcessingTimeSec = 42
        };

        _adminAlerts =
        [
            new AdminSystemAlert { Id = "alt1", Type = "Erreur systeme", Message = "Timeout moteur calcul sur lot #M2026-03-13", Severity = "Haute", Date = "2026-03-21 09:45" },
            new AdminSystemAlert { Id = "alt2", Type = "Incoherence", Message = "Scores manquants pour 3 employes (projet Alpha)", Severity = "Moyenne", Date = "2026-03-21 10:15" },
            new AdminSystemAlert { Id = "alt3", Type = "Workflow bloque", Message = "Validation RP en attente > SLA (48h)", Severity = "Moyenne", Date = "2026-03-21 11:02" }
        ];

        _adminCharts = new AdminDashboardCharts
        {
            VolumeByMonth =
            [
                new AdminChartPoint { Month = "Oct", Value = 980 },
                new AdminChartPoint { Month = "Nov", Value = 1050 },
                new AdminChartPoint { Month = "Dec", Value = 1125 },
                new AdminChartPoint { Month = "Jan", Value = 1180 },
                new AdminChartPoint { Month = "Feb", Value = 1260 },
                new AdminChartPoint { Month = "Mar", Value = 1486 }
            ],
            ValidationRate =
            [
                new AdminChartPoint { Month = "Oct", Value = 81 },
                new AdminChartPoint { Month = "Nov", Value = 84 },
                new AdminChartPoint { Month = "Dec", Value = 86 },
                new AdminChartPoint { Month = "Jan", Value = 88 },
                new AdminChartPoint { Month = "Feb", Value = 90 },
                new AdminChartPoint { Month = "Mar", Value = 92 }
            ],
            ByDepartment =
            [
                new AdminByDepartmentPoint { Name = "Operations", Value = 52 },
                new AdminByDepartmentPoint { Name = "IT", Value = 31 },
                new AdminByDepartmentPoint { Name = "RH", Value = 17 }
            ]
        };

        _adminCalculationConfig = new AdminCalculationConfig
        {
            Formula = "(ind_perf * w1) + (team_perf * w2) + (obj * w3) + bonus",
            Weights = new AdminCalculationWeights { IndividualPerformance = 50, TeamPerformance = 30, Objectives = 20 },
            Parameters = new AdminCalculationParameters { Cap = 1200, MinThreshold = 65, Bonus = 100 }
        };

        _adminRbacMatrix =
        [
            new AdminRbacRow { Role = "Admin", Read = true, Edit = true, Validate = true, Configure = true },
            new AdminRbacRow { Role = "RH", Read = true, Edit = true, Validate = true, Configure = false },
            new AdminRbacRow { Role = "Manager", Read = true, Edit = false, Validate = true, Configure = false },
            new AdminRbacRow { Role = "Superviseur", Read = true, Edit = false, Validate = true, Configure = false },
            new AdminRbacRow { Role = "RP", Read = true, Edit = false, Validate = true, Configure = false }
        ];

        _adminWorkflow = new AdminWorkflowConfig
        {
            Steps = ["Coach", "Superviseur", "Manager", "RP", "RH"],
            SlaHours = 48,
            NotificationsEnabled = true
        };

        _adminAuditLogs =
        [
            new AdminAuditLog { Id = "log1", User = "admin@mykyntus.com", Action = "Mise a jour formule de calcul", Date = "2026-03-22 14:04" },
            new AdminAuditLog { Id = "log2", User = "eve.thomas@mykyntus.com", Action = "Validation RH du lot M-2026-03", Date = "2026-03-22 15:16" },
            new AdminAuditLog { Id = "log3", User = "charlie.durand@mykyntus.com", Action = "Validation manager du lot M-2026-03", Date = "2026-03-22 16:28" }
        ];

        _adminAnomalies =
        [
            new AdminAnomaly { Id = "an1", Type = "Erreur de calcul", Description = "Division par zero detectee sur formule legacy", Status = "Ouverte" },
            new AdminAnomaly { Id = "an2", Type = "Donnee manquante", Description = "Objectif mensuel absent pour e2", Status = "Ouverte" },
            new AdminAnomaly { Id = "an3", Type = "Erreur de calcul", Description = "Score hors bornes sur lot M2026-02", Status = "Corrigee" }
        ];

        // Audit mock-data
        _auditKpis = new AuditKpis { TotalPrimes = 1486, Validations = 312, Anomalies = 7, ConformityRate = 93 };

        _auditCharts = new AuditDashboardCharts
        {
            FlowByStep =
            [
                new AuditFlowByStepPoint { Step = "Coach", Value = 220 },
                new AuditFlowByStepPoint { Step = "Superviseur", Value = 210 },
                new AuditFlowByStepPoint { Step = "Manager", Value = 200 },
                new AuditFlowByStepPoint { Step = "RP", Value = 180 },
                new AuditFlowByStepPoint { Step = "RH", Value = 170 }
            ],
            ValidationVsRejection =
            [
                new AuditNamedPoint { Name = "Validé", Value = 282 },
                new AuditNamedPoint { Name = "Rejeté", Value = 30 }
            ],
            ActivityByRole =
            [
                new AuditActivityByRolePoint { Role = "Coach", Value = 130 },
                new AuditActivityByRolePoint { Role = "Superviseur", Value = 125 },
                new AuditActivityByRolePoint { Role = "Manager", Value = 120 },
                new AuditActivityByRolePoint { Role = "RP", Value = 95 },
                new AuditActivityByRolePoint { Role = "RH", Value = 85 }
            ]
        };

        _auditOperations =
        [
            new AuditOperation
            {
                Id = "op1",
                EmployeeName = "Alice Dupont",
                ProjectName = "Projet Alpha",
                Steps =
                [
                    new AuditValidationStep { Role = "Manager", Status = "OK", Date = "2026-03-10T09:40:00.000Z" },
                    new AuditValidationStep { Role = "RP", Status = "OK", Date = "2026-03-11T10:05:00.000Z" },
                    new AuditValidationStep { Role = "RH", Status = "OK", Date = "2026-03-12T11:20:00.000Z" }
                ],
                ValidatedBy = "RH",
                Date = "2026-03-12",
                Status = "Validé"
            },
            new AuditOperation
            {
                Id = "op2",
                EmployeeName = "Bob Martin",
                ProjectName = "Projet Alpha",
                Steps =
                [
                    new AuditValidationStep { Role = "Manager", Status = "OK", Date = "2026-03-10T08:15:00.000Z" },
                    new AuditValidationStep { Role = "RP", Status = "REJECTED", Date = "2026-03-11T09:00:00.000Z" }
                ],
                ValidatedBy = "RP",
                Date = "2026-03-11",
                Status = "Rejeté"
            },
            new AuditOperation
            {
                Id = "op3",
                EmployeeName = "Diana Bernard",
                ProjectName = "Projet Alpha",
                Steps =
                [
                    new AuditValidationStep { Role = "Manager", Status = "OK", Date = "2026-03-07T07:35:00.000Z" },
                    new AuditValidationStep { Role = "RP", Status = "OK", Date = "2026-03-08T08:25:00.000Z" }
                ],
                ValidatedBy = "RP",
                Date = "2026-03-08",
                Status = "En cours"
            },
            new AuditOperation
            {
                Id = "op4",
                EmployeeName = "Bob Martin",
                ProjectName = "Projet Alpha",
                Steps =
                [
                    new AuditValidationStep { Role = "Manager", Status = "OK", Date = "2026-02-27T12:10:00.000Z" },
                    new AuditValidationStep { Role = "RP", Status = "OK", Date = "2026-02-28T13:05:00.000Z" },
                    new AuditValidationStep { Role = "RH", Status = "REJECTED", Date = "2026-02-28T14:55:00.000Z" }
                ],
                ValidatedBy = "RH",
                Date = "2026-02-28",
                Status = "Rejeté"
            },
            new AuditOperation
            {
                Id = "op5",
                EmployeeName = "Alice Dupont",
                ProjectName = "Projet Alpha",
                Steps =
                [
                    new AuditValidationStep { Role = "Manager", Status = "OK", Date = "2026-02-18T10:05:00.000Z" }
                ],
                ValidatedBy = "Manager",
                Date = "2026-02-18",
                Status = "En cours"
            }
        ];

        _auditTrailLogs =
        [
            new AuditTrailLog { Id = "log-a1", User = "salma.bennani@mykyntus.com", Action = "Audit: consultation et export", Date = "2026-03-22T09:12:00.000Z", Detail = "Lecture des opérations pour le périmètre Projet Alpha." },
            new AuditTrailLog { Id = "log-a2", User = "charlie.durand@mykyntus.com", Action = "Workflow: validation Manager", Date = "2026-03-10T09:40:00.000Z", Detail = "Validation de l’étape Manager sur op1." },
            new AuditTrailLog { Id = "log-a3", User = "rachid.elamrani@mykyntus.com", Action = "Workflow: validation RP", Date = "2026-03-11T10:05:00.000Z", Detail = "Validation de l’étape RP sur op1." },
            new AuditTrailLog { Id = "log-a4", User = "eve.thomas@mykyntus.com", Action = "Workflow: validation RH", Date = "2026-03-12T11:20:00.000Z", Detail = "Validation de l’étape RH sur op1." }
        ];

        _auditAnomalies =
        [
            new AuditAnomaly { Id = "anom-1", Type = "Incohérence", Description = "Une étape RP manquante a été détectée après validation Manager.", ValidationId = "op3", Status = "Ouverte" },
            new AuditAnomaly { Id = "anom-2", Type = "Erreur de calcul", Description = "Score hors bornes sur l’intervalle 2026-02.", ValidationId = "op4", Status = "Ouverte" },
            new AuditAnomaly { Id = "anom-3", Type = "Validation manquante", Description = "Validation RH non enregistrée sur op5.", ValidationId = "op5", Status = "Ouverte" },
            new AuditAnomaly { Id = "anom-4", Type = "Incohérence", Description = "Rejet sans motif détaillé disponible.", ValidationId = "op2", Status = "Corrigée" }
        ];
    }

    // Prime getters
    public List<Department> GetDepartments() => _departments;
    public List<Employee> GetEmployees() => _employees;
    public List<PrimeType> GetPrimeTypes() => _primeTypes;
    public List<PrimeRule> GetPrimeRules() => _primeRules;
    public List<PrimeResult> GetPrimeResults() => _primeResults;

    public List<PrimeResult> GetMyPrimeResults(string employeeId) =>
        _primeResults.Where(r => r.EmployeeId == employeeId).ToList();

    public PrimeResult UpdatePrimeResultStatus(string id, string status, string? approvedBy)
    {
        var result = _primeResults.FirstOrDefault(r => r.Id == id);
        if (result is null) throw new KeyNotFoundException("Result not found");
        if (!IsAllowedStatusTransition(result.Status, status))
            throw new InvalidOperationException("Transition de statut non autorisée.");
        result.Status = status;
        if (!string.IsNullOrWhiteSpace(approvedBy)) result.ApprovedBy = approvedBy;
        return result;
    }

    private static bool IsAllowedStatusTransition(string current, string next)
    {
        if (string.Equals(current, next, StringComparison.Ordinal)) return true;
        if (string.Equals(next, "Rejected", StringComparison.Ordinal)) return true;
        if (string.Equals(next, "RH Approved", StringComparison.Ordinal)) return true; // RH/Admin final approval

        return current switch
        {
            "Pending" => next == "Coach Approved",
            "Coach Approved" => next == "Superviseur Approved",
            "Superviseur Approved" => next == "Manager Approved",
            "Manager Approved" => next == "RP Approved",
            "RP Approved" => next == "RH Approved",
            _ => false,
        };
    }

    // Organization getters + assignments
    public List<EtageNode> GetEtages() => _etages;
    public List<ServiceNode> GetServices() => _services;
    public List<SousServiceNode> GetSousServices() => _sousServices;

    public List<ManagerEtageAssignment> GetManagerEtageAssignments(string? userId = null) =>
        string.IsNullOrWhiteSpace(userId)
            ? _managerEtageAssignments
            : _managerEtageAssignments.Where(a => a.UserId == userId.Trim()).ToList();

    public List<SupervisorServiceAssignment> GetSupervisorServiceAssignments(string? userId = null) =>
        string.IsNullOrWhiteSpace(userId)
            ? _supervisorServiceAssignments
            : _supervisorServiceAssignments.Where(a => a.UserId == userId.Trim()).ToList();

    public List<CoachSousServiceAssignment> GetCoachSousServiceAssignments(string? userId = null) =>
        string.IsNullOrWhiteSpace(userId)
            ? _coachSousServiceAssignments
            : _coachSousServiceAssignments.Where(a => a.UserId == userId.Trim()).ToList();

    public List<CoachPilotLink> GetCoachPilotLinks(string? coachUserId = null) =>
        string.IsNullOrWhiteSpace(coachUserId)
            ? _coachPilotLinks
            : _coachPilotLinks.Where(a => a.CoachUserId == coachUserId.Trim()).ToList();

    public ManagerEtageAssignment AssignManagerEtage(string userId, string etageId)
    {
        if (!_etages.Any(e => e.Id == etageId)) throw new KeyNotFoundException("Etage introuvable");
        if (_managerEtageAssignments.Any(a => a.UserId == userId && a.EtageId == etageId))
            throw new InvalidOperationException("Déjà assigné.");
        var created = new ManagerEtageAssignment { Id = Guid.NewGuid().ToString("N"), UserId = userId.Trim(), EtageId = etageId.Trim() };
        _managerEtageAssignments.Add(created);
        return created;
    }

    public SupervisorServiceAssignment AssignSupervisorService(string userId, string serviceId)
    {
        if (!_services.Any(s => s.Id == serviceId)) throw new KeyNotFoundException("Service introuvable");
        if (_supervisorServiceAssignments.Any(a => a.UserId == userId && a.ServiceId == serviceId))
            throw new InvalidOperationException("Déjà assigné.");
        var created = new SupervisorServiceAssignment { Id = Guid.NewGuid().ToString("N"), UserId = userId.Trim(), ServiceId = serviceId.Trim() };
        _supervisorServiceAssignments.Add(created);
        return created;
    }

    public CoachSousServiceAssignment AssignCoachSousService(string userId, string sousServiceId)
    {
        if (!_sousServices.Any(s => s.Id == sousServiceId)) throw new KeyNotFoundException("Sous-service introuvable");
        if (_coachSousServiceAssignments.Any(a => a.UserId == userId && a.SousServiceId == sousServiceId))
            throw new InvalidOperationException("Déjà assigné.");
        var created = new CoachSousServiceAssignment { Id = Guid.NewGuid().ToString("N"), UserId = userId.Trim(), SousServiceId = sousServiceId.Trim() };
        _coachSousServiceAssignments.Add(created);
        return created;
    }

    public CoachPilotLink AssignCoachPilot(string coachUserId, string pilotUserId)
    {
        if (_coachPilotLinks.Any(a => a.CoachUserId == coachUserId && a.PilotUserId == pilotUserId))
            throw new InvalidOperationException("Déjà lié.");
        var created = new CoachPilotLink { Id = Guid.NewGuid().ToString("N"), CoachUserId = coachUserId.Trim(), PilotUserId = pilotUserId.Trim() };
        _coachPilotLinks.Add(created);
        return created;
    }

    public void RemoveManagerEtageAssignment(string assignmentId)
    {
        var row = _managerEtageAssignments.FirstOrDefault(a => a.Id == assignmentId);
        if (row is null) throw new KeyNotFoundException("Affectation manager / département introuvable.");
        _managerEtageAssignments.Remove(row);
    }

    public void RemoveSupervisorServiceAssignment(string assignmentId)
    {
        var row = _supervisorServiceAssignments.FirstOrDefault(a => a.Id == assignmentId);
        if (row is null) throw new KeyNotFoundException("Affectation superviseur / pôle introuvable.");
        _supervisorServiceAssignments.Remove(row);
    }

    public void RemoveCoachSousServiceAssignment(string assignmentId)
    {
        var row = _coachSousServiceAssignments.FirstOrDefault(a => a.Id == assignmentId);
        if (row is null) throw new KeyNotFoundException("Affectation coach / cellule introuvable.");
        _coachSousServiceAssignments.Remove(row);
    }

    public void RemoveCoachPilotLink(string linkId)
    {
        var row = _coachPilotLinks.FirstOrDefault(a => a.Id == linkId);
        if (row is null) throw new KeyNotFoundException("Lien coach / pilote introuvable.");
        _coachPilotLinks.Remove(row);
    }

    // --- RH : affectations par arbre (rôles dérivés). Règles métier : OrgStructureRules. ---

    private Employee RequireEmployee(string employeeId)
    {
        var e = _employees.FirstOrDefault(x => x.Id == employeeId.Trim());
        if (e is null) throw new KeyNotFoundException("Employé introuvable.");
        return e;
    }

    private static bool IsProtectedStructureRole(string role) =>
        role is "RH" or "Admin" or "Audit" or "RP";

    private Department RequireDepartment(string departmentId)
    {
        var d = _departments.FirstOrDefault(x => x.Id == departmentId.Trim());
        if (d is null) throw new KeyNotFoundException("Département introuvable.");
        return d;
    }

    /// <summary>Premier pôle, première cellule, première équipe du département (ordre stable des listes).</summary>
    private static (Department Dept, Pole Pole, Cellule Cell, Team Team) GetFirstTeamAnchorInDepartment(Department dept)
    {
        var pole = dept.Poles.FirstOrDefault() ?? throw new InvalidOperationException("Département sans pôle.");
        var cell = pole.Cells.FirstOrDefault() ?? throw new InvalidOperationException("Pôle sans cellule.");
        var team = cell.Teams.FirstOrDefault() ?? throw new InvalidOperationException("Cellule sans équipe.");
        return (dept, pole, cell, team);
    }

    private (Department Dept, Pole Pole, Cellule Cell, Team Team) GetFirstTeamPathInDepartment(string departmentId) =>
        GetFirstTeamAnchorInDepartment(RequireDepartment(departmentId));

    private (Department Dept, Pole Pole, Cellule Cell, Team Team) ResolveCellulePath(string celluleId)
    {
        foreach (var d in _departments)
        {
            foreach (var p in d.Poles)
            {
                var c = p.Cells.FirstOrDefault(x => x.Id == celluleId.Trim());
                if (c is null) continue;
                var team = c.Teams.FirstOrDefault() ?? throw new InvalidOperationException("Cellule sans équipe.");
                return (d, p, c, team);
            }
        }
        throw new KeyNotFoundException("Cellule introuvable.");
    }

    private (Department Dept, Pole Pole, Cellule Cell, Team Team) GetFirstTeamPathInPole(string poleId)
    {
        var (dept, pole) = GetDepartmentForPole(poleId);
        var cell = pole.Cells.FirstOrDefault() ?? throw new InvalidOperationException("Pôle sans cellule.");
        var team = cell.Teams.FirstOrDefault() ?? throw new InvalidOperationException("Cellule sans équipe.");
        return (dept, pole, cell, team);
    }

    private (Department Dept, Pole Pole) GetDepartmentForPole(string poleId)
    {
        foreach (var d in _departments)
        {
            var pole = d.Poles.FirstOrDefault(p => p.Id == poleId.Trim());
            if (pole is not null) return (d, pole);
        }
        throw new KeyNotFoundException("Pôle introuvable.");
    }

    private Team ResolveTeamInCellule(string celluleId, string? teamId)
    {
        var (_, _, cell, defaultTeam) = ResolveCellulePath(celluleId);
        if (string.IsNullOrWhiteSpace(teamId)) return defaultTeam;
        var t = cell.Teams.FirstOrDefault(x => x.Id == teamId.Trim());
        if (t is null) throw new KeyNotFoundException("Équipe introuvable pour cette cellule.");
        return t;
    }

    private string? GetFirstRpId() => _employees.FirstOrDefault(e => e.Role == "RP")?.Id;

    private string? GetManagerUserIdForDepartment(string departmentId) =>
        _managerEtageAssignments.FirstOrDefault(a => a.EtageId == departmentId)?.UserId;

    private string? GetSupervisorUserIdForPole(string poleId) =>
        _supervisorServiceAssignments.FirstOrDefault(a => a.ServiceId == poleId)?.UserId;

    private string? GetCoachUserIdForCellule(string celluleId) =>
        _coachSousServiceAssignments.FirstOrDefault(a => a.SousServiceId == celluleId)?.UserId;

    /// <summary>Retire toutes les lignes d’affectation structurelle impliquant cet utilisateur (manager, superviseur, coach, liens coach–pilote).</summary>
    private void StripOrgStructureAssignmentsForUser(string userId)
    {
        var uid = userId.Trim();
        _managerEtageAssignments.RemoveAll(a => a.UserId == uid);
        _supervisorServiceAssignments.RemoveAll(a => a.UserId == uid);
        _coachSousServiceAssignments.RemoveAll(a => a.UserId == uid);
        _coachPilotLinks.RemoveAll(a => a.CoachUserId == uid || a.PilotUserId == uid);
    }

    /// <summary>Remplace le manager du département (un seul actif) et aligne rôle / périmètre / parent RP.</summary>
    public void SetManagerForDepartment(string employeeId, string departmentId)
    {
        var emp = RequireEmployee(employeeId);
        if (IsProtectedStructureRole(emp.Role))
            throw new InvalidOperationException("Ce profil (RH / Admin / Audit) ne peut pas recevoir une affectation structurelle.");
        var dept = RequireDepartment(departmentId);
        var deptKey = departmentId.Trim();

        foreach (var row in _managerEtageAssignments.Where(a => a.EtageId == deptKey).ToList())
        {
            if (string.Equals(row.UserId, emp.Id, StringComparison.Ordinal)) continue;
            var old = RequireEmployee(row.UserId);
            var (d0, p0, c0, t0) = GetFirstTeamPathInDepartment(deptKey);
            var coach = GetCoachUserIdForCellule(c0.Id);
            var sup = GetSupervisorUserIdForPole(p0.Id);
            old.Role = "Pilote";
            old.DepartementId = d0.Id;
            old.PoleId = p0.Id;
            old.CelluleId = c0.Id;
            old.TeamId = t0.Id;
            old.ParentId = coach ?? sup;
            _managerEtageAssignments.Remove(row);
        }

        StripOrgStructureAssignmentsForUser(emp.Id);

        var (d, p, c, t) = GetFirstTeamAnchorInDepartment(dept);
        emp.Role = "Manager";
        emp.DepartementId = d.Id;
        emp.PoleId = p.Id;
        emp.CelluleId = c.Id;
        emp.TeamId = t.Id;
        emp.ParentId = GetFirstRpId();

        if (!_managerEtageAssignments.Any(a => a.UserId == emp.Id && a.EtageId == deptKey))
            _managerEtageAssignments.Add(new ManagerEtageAssignment { Id = Guid.NewGuid().ToString("N"), UserId = emp.Id, EtageId = deptKey });
    }

    public void ClearManagerForDepartment(string departmentId)
    {
        var deptKey = departmentId.Trim();
        var row = _managerEtageAssignments.FirstOrDefault(a => a.EtageId == deptKey);
        if (row is null) return;
        var old = RequireEmployee(row.UserId);
        var (d0, p0, c0, t0) = GetFirstTeamPathInDepartment(deptKey);
        var coach = GetCoachUserIdForCellule(c0.Id);
        var sup = GetSupervisorUserIdForPole(p0.Id);
        old.Role = "Pilote";
        old.DepartementId = d0.Id;
        old.PoleId = p0.Id;
        old.CelluleId = c0.Id;
        old.TeamId = t0.Id;
        old.ParentId = coach ?? sup;
        _managerEtageAssignments.Remove(row);
    }

    /// <summary>Remplace le superviseur du pôle (un seul actif).</summary>
    public void SetSupervisorForPole(string employeeId, string poleId)
    {
        var emp = RequireEmployee(employeeId);
        if (IsProtectedStructureRole(emp.Role))
            throw new InvalidOperationException("Ce profil (RH / Admin / Audit) ne peut pas recevoir une affectation structurelle.");
        var (dept, pole) = GetDepartmentForPole(poleId);
        var poleKey = pole.Id;

        foreach (var row in _supervisorServiceAssignments.Where(a => a.ServiceId == poleKey).ToList())
        {
            if (string.Equals(row.UserId, emp.Id, StringComparison.Ordinal)) continue;
            var old = RequireEmployee(row.UserId);
            var cell0 = pole.Cells.FirstOrDefault() ?? throw new InvalidOperationException("Pôle sans cellule.");
            var team0 = cell0.Teams.FirstOrDefault() ?? throw new InvalidOperationException("Cellule sans équipe.");
            var coach = GetCoachUserIdForCellule(cell0.Id);
            old.Role = "Pilote";
            old.DepartementId = dept.Id;
            old.PoleId = pole.Id;
            old.CelluleId = cell0.Id;
            old.TeamId = team0.Id;
            old.ParentId = coach ?? GetManagerUserIdForDepartment(dept.Id);
            _supervisorServiceAssignments.Remove(row);
        }

        StripOrgStructureAssignmentsForUser(emp.Id);

        var cell = pole.Cells.FirstOrDefault() ?? throw new InvalidOperationException("Pôle sans cellule.");
        var team = cell.Teams.FirstOrDefault() ?? throw new InvalidOperationException("Cellule sans équipe.");
        emp.Role = "Superviseur";
        emp.DepartementId = dept.Id;
        emp.PoleId = pole.Id;
        emp.CelluleId = cell.Id;
        emp.TeamId = team.Id;
        emp.ParentId = GetManagerUserIdForDepartment(dept.Id);

        if (!_supervisorServiceAssignments.Any(a => a.UserId == emp.Id && a.ServiceId == poleKey))
            _supervisorServiceAssignments.Add(new SupervisorServiceAssignment { Id = Guid.NewGuid().ToString("N"), UserId = emp.Id, ServiceId = poleKey });
    }

    public void ClearSupervisorForPole(string poleId)
    {
        var poleKey = poleId.Trim();
        var row = _supervisorServiceAssignments.FirstOrDefault(a => a.ServiceId == poleKey);
        if (row is null) return;
        var (dept, pole) = GetDepartmentForPole(poleKey);
        var old = RequireEmployee(row.UserId);
        var cell0 = pole.Cells.FirstOrDefault() ?? throw new InvalidOperationException("Pôle sans cellule.");
        var team0 = cell0.Teams.FirstOrDefault() ?? throw new InvalidOperationException("Cellule sans équipe.");
        var coach = GetCoachUserIdForCellule(cell0.Id);
        old.Role = "Pilote";
        old.DepartementId = dept.Id;
        old.PoleId = pole.Id;
        old.CelluleId = cell0.Id;
        old.TeamId = team0.Id;
        old.ParentId = coach ?? GetManagerUserIdForDepartment(dept.Id);
        _supervisorServiceAssignments.Remove(row);
    }

    /// <summary>Remplace le coach de la cellule (un seul actif).</summary>
    public void SetCoachForCellule(string employeeId, string celluleId)
    {
        var emp = RequireEmployee(employeeId);
        if (IsProtectedStructureRole(emp.Role))
            throw new InvalidOperationException("Ce profil (RH / Admin / Audit) ne peut pas recevoir une affectation structurelle.");
        var (dept, pole, cell, team) = ResolveCellulePath(celluleId);
        var cellKey = cell.Id;

        foreach (var row in _coachSousServiceAssignments.Where(a => a.SousServiceId == cellKey).ToList())
        {
            if (string.Equals(row.UserId, emp.Id, StringComparison.Ordinal)) continue;
            var old = RequireEmployee(row.UserId);
            old.Role = "Pilote";
            old.DepartementId = dept.Id;
            old.PoleId = pole.Id;
            old.CelluleId = cell.Id;
            old.TeamId = team.Id;
            old.ParentId = GetSupervisorUserIdForPole(pole.Id);
            _coachSousServiceAssignments.Remove(row);
        }

        StripOrgStructureAssignmentsForUser(emp.Id);

        emp.Role = "Coach";
        emp.DepartementId = dept.Id;
        emp.PoleId = pole.Id;
        emp.CelluleId = cell.Id;
        emp.TeamId = team.Id;
        emp.ParentId = GetSupervisorUserIdForPole(pole.Id);

        if (!_coachSousServiceAssignments.Any(a => a.UserId == emp.Id && a.SousServiceId == cellKey))
            _coachSousServiceAssignments.Add(new CoachSousServiceAssignment { Id = Guid.NewGuid().ToString("N"), UserId = emp.Id, SousServiceId = cellKey });
    }

    public void ClearCoachForCellule(string celluleId)
    {
        var (dept, pole, cell, team) = ResolveCellulePath(celluleId);
        var row = _coachSousServiceAssignments.FirstOrDefault(a => a.SousServiceId == cell.Id);
        if (row is null) return;
        var old = RequireEmployee(row.UserId);
        old.Role = "Pilote";
        old.DepartementId = dept.Id;
        old.PoleId = pole.Id;
        old.CelluleId = cell.Id;
        old.TeamId = team.Id;
        old.ParentId = GetSupervisorUserIdForPole(pole.Id);
        _coachSousServiceAssignments.Remove(row);
    }

    /// <summary>Ajoute ou déplace un pilote sur la cellule (lien coach–pilote + parentId coach). Nécessite un coach affecté.</summary>
    public void AddPilotToCellule(string employeeId, string celluleId, string? teamId = null)
    {
        var emp = RequireEmployee(employeeId);
        if (IsProtectedStructureRole(emp.Role))
            throw new InvalidOperationException("Ce profil (RH / Admin / Audit) ne peut pas être défini comme pilote ici.");
        var coachId = GetCoachUserIdForCellule(celluleId.Trim());
        if (string.IsNullOrWhiteSpace(coachId))
            throw new InvalidOperationException("Affectez d’abord un coach à cette cellule avant d’ajouter un pilote.");

        var team = ResolveTeamInCellule(celluleId, teamId);
        var (dept, pole, cell, _) = ResolveCellulePath(celluleId);
        if (cell.Id != team.CelluleId) throw new InvalidOperationException("L’équipe ne correspond pas à la cellule.");

        StripOrgStructureAssignmentsForUser(emp.Id);
        _coachPilotLinks.RemoveAll(a => a.PilotUserId == emp.Id);

        emp.Role = "Pilote";
        emp.DepartementId = dept.Id;
        emp.PoleId = pole.Id;
        emp.CelluleId = cell.Id;
        emp.TeamId = team.Id;
        emp.ParentId = coachId;

        if (!_coachPilotLinks.Any(a => a.CoachUserId == coachId && a.PilotUserId == emp.Id))
            _coachPilotLinks.Add(new CoachPilotLink { Id = Guid.NewGuid().ToString("N"), CoachUserId = coachId, PilotUserId = emp.Id });
    }

    public void RemovePilotFromCellule(string employeeId, string celluleId)
    {
        var emp = RequireEmployee(employeeId);
        var (_, _, cell, _) = ResolveCellulePath(celluleId);
        if (!string.Equals(emp.CelluleId, cell.Id, StringComparison.Ordinal))
            throw new InvalidOperationException("Ce pilote n’est pas rattaché à cette cellule.");
        _coachPilotLinks.RemoveAll(a => a.PilotUserId == emp.Id);
        emp.ParentId = null;
    }

    public List<SupervisorPrimeRow> GetSupervisorPrimes(string supervisorUserId, string? period = null)
    {
        EnsureSupervisor(supervisorUserId);
        var allowedServiceIds = _supervisorServiceAssignments
            .Where(a => a.UserId == supervisorUserId)
            .Select(a => a.ServiceId)
            .ToHashSet();

        var visibleEmployeeIds = _employees
            .Where(e => allowedServiceIds.Contains(e.PoleId))
            .Select(e => e.Id)
            .ToHashSet();

        var query = _primeResults.Where(r => visibleEmployeeIds.Contains(r.EmployeeId));
        if (!string.IsNullOrWhiteSpace(period))
        {
            query = query.Where(r => r.Period == period.Trim());
        }

        return query
            .Select(r =>
            {
                var employee = _employees.FirstOrDefault(e => e.Id == r.EmployeeId);
                return new SupervisorPrimeRow
                {
                    Id = r.Id,
                    EmployeeId = r.EmployeeId,
                    EmployeeName = employee is null ? r.EmployeeId : $"{employee.FirstName} {employee.LastName}",
                    Status = r.Status,
                    Amount = r.Amount,
                    Score = r.Score,
                    Period = r.Period,
                };
            })
            .ToList();
    }

    public SupervisorPrimeRow ValidateAsSupervisor(string supervisorUserId, string resultId)
    {
        EnsureSupervisor(supervisorUserId);
        var result = GetScopedResultOrThrow(supervisorUserId, resultId);
        if (result.Status != "Coach Approved") throw new InvalidOperationException("Seules les fiches Coach Approved sont validables.");
        result.Status = "Superviseur Approved";
        result.ApprovedBy = supervisorUserId;
        return MapSupervisorPrimeRow(result);
    }

    public SupervisorPrimeRow RejectAsSupervisor(string supervisorUserId, string resultId)
    {
        EnsureSupervisor(supervisorUserId);
        var result = GetScopedResultOrThrow(supervisorUserId, resultId);
        if (result.Status is "RH Approved" or "RP Approved") throw new InvalidOperationException("Fiche terminale non rejetable.");
        result.Status = "Rejected";
        result.ApprovedBy = supervisorUserId;
        return MapSupervisorPrimeRow(result);
    }

    public SupervisorCalculateResponse ComputePrimeSupervisor(SupervisorCalculateRequest req)
    {
        EnsureSupervisor(req.SupervisorUserId);
        _ = GetScopedResultOrThrow(req.SupervisorUserId, req.ResultId);

        var boundedCoefficient = req.Coefficient <= 0 ? 1m : req.Coefficient;
        var effectivePenalty = Math.Max(0m, req.Penalty);
        var effectiveBonus = Math.Max(0m, req.Bonus);
        var globalScore = Math.Max(0m, req.Score * boundedCoefficient - effectivePenalty + effectiveBonus);
        var finalAmount = Math.Max(0m, req.BaseAmount * boundedCoefficient - effectivePenalty + effectiveBonus);

        return new SupervisorCalculateResponse
        {
            GlobalScore = (int)Math.Round(globalScore),
            FinalAmount = (int)Math.Round(finalAmount),
            PenaltyApplied = (int)Math.Round(effectivePenalty),
            BonusApplied = (int)Math.Round(effectiveBonus)
        };
    }

    public SupervisorDashboardResponse GetSupervisorDashboard(string supervisorUserId)
    {
        var rows = GetSupervisorPrimes(supervisorUserId);
        return new SupervisorDashboardResponse
        {
            Pending = rows.Count(r => r.Status == "Coach Approved"),
            Approved = rows.Count(r => r.Status == "Superviseur Approved"),
            Rejected = rows.Count(r => r.Status == "Rejected"),
            Anomalies = rows.Count(r => r.Amount <= 0 || r.Score <= 0)
        };
    }

    private PrimeResult GetScopedResultOrThrow(string supervisorUserId, string resultId)
    {
        var scopedIds = GetSupervisorPrimes(supervisorUserId).Select(r => r.Id).ToHashSet();
        if (!scopedIds.Contains(resultId)) throw new UnauthorizedAccessException("Résultat hors périmètre superviseur.");
        var result = _primeResults.FirstOrDefault(r => r.Id == resultId);
        if (result is null) throw new KeyNotFoundException("Result introuvable.");
        return result;
    }

    private SupervisorPrimeRow MapSupervisorPrimeRow(PrimeResult result)
    {
        var employee = _employees.FirstOrDefault(e => e.Id == result.EmployeeId);
        return new SupervisorPrimeRow
        {
            Id = result.Id,
            EmployeeId = result.EmployeeId,
            EmployeeName = employee is null ? result.EmployeeId : $"{employee.FirstName} {employee.LastName}",
            Status = result.Status,
            Amount = result.Amount,
            Score = result.Score,
            Period = result.Period,
        };
    }

    private void EnsureSupervisor(string supervisorUserId)
    {
        var user = _employees.FirstOrDefault(e => e.Id == supervisorUserId);
        if (user is null) throw new KeyNotFoundException("Utilisateur superviseur introuvable.");
        if (!string.Equals(user.Role, "Superviseur", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Rôle non autorisé.");
    }

    public object GetPrimeDashboardStats() => new
    {
        totalPrimesThisMonth = 1250,
        budgetConsumption = 75,
        topTeams = new[]
        {
            new { name = "Team Alpha", amount = 4500 },
            new { name = "Team Gamma", amount = 3200 },
            new { name = "Team Beta", amount = 2800 },
        },
        topEmployees = new[]
        {
            new { name = "Alice Dupont", amount = 800 },
            new { name = "Diana Bernard", amount = 500 },
            new { name = "Bob Martin", amount = 450 },
        },
        primeByDepartment = new[]
        {
            new { name = "Operations", value = 8500 },
            new { name = "IT / Technical", value = 1200 },
            new { name = "HR", value = 400 },
        },
        primeEvolution = new[]
        {
            new { month = "Oct", amount = 7000 },
            new { month = "Nov", amount = 8200 },
            new { month = "Dec", amount = 9500 },
            new { month = "Jan", amount = 8800 },
            new { month = "Feb", amount = 9100 },
            new { month = "Mar", amount = 10100 },
        }
    };

    // RP getters + computation
    public List<string> GetAssignedProjectIds(string rpUserId) =>
        _rpProjectAssignments.TryGetValue(rpUserId, out var list) && list.Count > 0 ? list : ["proj-alpha"];

    public RpDashboardStats GetRpDashboardStats(string rpUserId)
    {
        var projectIds = GetAssignedProjectIds(rpUserId);

        var projectTeamData = _rpTeamPerformance.Where(x => projectIds.Contains(x.ProjectId)).ToList();
        var projectValidationData = _rpValidationItems.Where(x => projectIds.Contains(x.ProjectId)).ToList();

        var totalCompletedTasks = projectTeamData.Sum(m => m.CompletedTasks);
        var totalTasks = projectTeamData.Sum(m => m.TotalTasks);
        var avgTeamPerformance = projectTeamData.Count == 0
            ? 0
            : (int)Math.Round(
                projectTeamData.Sum(member =>
                    Math.Round((member.CompletedTasks / Math.Max(member.TotalTasks, 1.0)) * 60
                               + (member.ObjectivesReached / Math.Max(member.TotalObjectives, 1.0)) * 40)
                )
                / (double)projectTeamData.Count
            );

        List<MonthScore> performanceEvolution = [];
        if (projectTeamData.Count > 0 && projectTeamData[0].MonthlyPerformance.Count > 0)
        {
            for (int index = 0; index < projectTeamData[0].MonthlyPerformance.Count; index++)
            {
                var monthScores = projectTeamData.Sum(m => m.MonthlyPerformance[index].Score);
                var monthAverage = monthScores / (double)projectTeamData.Count;
                performanceEvolution.Add(new MonthScore
                {
                    Month = projectTeamData[0].MonthlyPerformance[index].Month,
                    Score = (int)Math.Round(monthAverage)
                });
            }
        }

        var memberPerformance = projectTeamData.Select(member =>
        {
            var score = (int)Math.Round(
                (member.CompletedTasks / (double)Math.Max(member.TotalTasks, 1)) * 60
              + (member.ObjectivesReached / (double)Math.Max(member.TotalObjectives, 1)) * 40
            );
            var status = score >= 85 ? "Excellent" : score >= 70 ? "Moyen" : "Faible";
            return new MemberPerformance { Name = member.EmployeeName, Score = score, Status = status };
        }).ToList();

        return new RpDashboardStats
        {
            ProjectProgress = (int)Math.Round((totalCompletedTasks / (double)Math.Max(totalTasks, 1)) * 100),
            CompletedTasks = totalCompletedTasks,
            AverageTeamPerformance = avgTeamPerformance,
            PendingValidations = projectValidationData.Count(v => v.Status == "Manager Approved"),
            PerformanceEvolution = performanceEvolution,
            MemberPerformance = memberPerformance
        };
    }

    public List<RpTeamMemberPerformance> GetTeamPerformanceByProject(string rpUserId)
    {
        var projectIds = GetAssignedProjectIds(rpUserId);
        return _rpTeamPerformance.Where(x => projectIds.Contains(x.ProjectId)).ToList();
    }

    public List<RpValidationItem> GetManagerValidatedPrimes(string rpUserId)
    {
        var projectIds = GetAssignedProjectIds(rpUserId);
        return _rpValidationItems
            .Where(x => projectIds.Contains(x.ProjectId))
            .Where(x => x.ManagerValidated)
            .Select(x => x)
            .ToList();
    }

    public RpValidationItem UpdateRpValidationStatus(string id, string status)
    {
        var item = _rpValidationItems.FirstOrDefault(x => x.Id == id);
        if (item is null) throw new KeyNotFoundException("Validation introuvable");
        item.Status = status;
        return item;
    }

    // Admin getters + updates
    public AdminDashboardResponse GetAdminDashboard() =>
        new AdminDashboardResponse
        {
            Kpis = _adminSystemKpis,
            Charts = _adminCharts,
            Alerts = _adminAlerts
        };

    public AdminCalculationConfig GetCalculationConfig() => _adminCalculationConfig;

    public AdminCalculationConfig SaveCalculationConfig(AdminCalculationConfig payload)
    {
        _adminCalculationConfig.Formula = payload.Formula;
        _adminCalculationConfig.Weights = payload.Weights;
        _adminCalculationConfig.Parameters = payload.Parameters;
        return _adminCalculationConfig;
    }

    public List<AdminRbacRow> GetRbacMatrix() => _adminRbacMatrix;

    public List<AdminRbacRow> ToggleRbacPermission(string role, string permission)
    {
        var row = _adminRbacMatrix.FirstOrDefault(r => string.Equals(r.Role, role, StringComparison.OrdinalIgnoreCase));
        if (row is null) throw new KeyNotFoundException("Role introuvable");

        permission = (permission ?? "").Trim().ToLowerInvariant();
        switch (permission)
        {
            case "read": row.Read = !row.Read; break;
            case "edit": row.Edit = !row.Edit; break;
            case "validate": row.Validate = !row.Validate; break;
            case "configure": row.Configure = !row.Configure; break;
            default: throw new ArgumentException("Unknown permission");
        }
        return _adminRbacMatrix;
    }

    public AdminWorkflowConfig GetWorkflowConfig() => _adminWorkflow;

    public AdminWorkflowConfig SaveWorkflowConfig(AdminWorkflowConfig payload)
    {
        _adminWorkflow.Steps = payload.Steps;
        _adminWorkflow.SlaHours = payload.SlaHours;
        _adminWorkflow.NotificationsEnabled = payload.NotificationsEnabled;
        return _adminWorkflow;
    }

    public List<AdminAuditLog> GetAuditLogs() => _adminAuditLogs;

    public List<AdminAnomaly> GetAdminAnomalies() => _adminAnomalies;

    public List<AdminAnomaly> UpdateAnomalyStatus(string id, string status)
    {
        var row = _adminAnomalies.FirstOrDefault(x => x.Id == id);
        if (row is null) throw new KeyNotFoundException("Anomalie introuvable");
        row.Status = status;
        return _adminAnomalies;
    }

    // Audit getters
    public AuditDashboardResponse GetAuditDashboard() =>
        new AuditDashboardResponse { Kpis = _auditKpis, Charts = _auditCharts };

    public List<AuditOperation> GetOperations() => _auditOperations;
    public List<AuditTrailLog> GetAuditTrailLogs() => _auditTrailLogs;
    public List<AuditAnomaly> GetAuditAnomalies() => _auditAnomalies;

    public List<PrimeConfigItem> GetPrimeConfigs(string? kind, string? sector, string? groupCode, string? activityType)
    {
        var q = _primeConfigs.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(kind)) q = q.Where(x => x.Kind.Equals(kind.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(sector)) q = q.Where(x => x.Sector.Equals(sector.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(groupCode)) q = q.Where(x => x.GroupCode.Equals(groupCode.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(activityType)) q = q.Where(x => x.ActivityType.Equals(activityType.Trim(), StringComparison.OrdinalIgnoreCase));
        return q.ToList();
    }

    public PrimeConfigItem CreatePrimeConfig(PrimeConfigUpsertRequest req)
    {
        PrimeConfigValidator.ValidateOrThrow(req);
        var created = new PrimeConfigItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = req.Kind.Trim(),
            Sector = req.Sector.Trim(),
            GroupCode = req.GroupCode.Trim(),
            ActivityType = req.ActivityType.Trim(),
            Label = req.Label?.Trim(),
            Min = req.Min,
            Max = req.Max,
            InvertedLogic = req.InvertedLogic,
            Weight = req.Weight,
            PrimeCap = req.PrimeCap,
            ChallengeCap = req.ChallengeCap
        };
        _primeConfigs.Add(created);
        return created;
    }

    public PrimeConfigItem UpdatePrimeConfig(string id, PrimeConfigUpsertRequest req)
    {
        PrimeConfigValidator.ValidateOrThrow(req);
        var row = _primeConfigs.FirstOrDefault(x => x.Id == id);
        if (row is null) throw new KeyNotFoundException("Configuration introuvable");
        row.Kind = req.Kind.Trim();
        row.Sector = req.Sector.Trim();
        row.GroupCode = req.GroupCode.Trim();
        row.ActivityType = req.ActivityType.Trim();
        row.Label = req.Label?.Trim();
        row.Min = req.Min;
        row.Max = req.Max;
        row.InvertedLogic = req.InvertedLogic;
        row.Weight = req.Weight;
        row.PrimeCap = req.PrimeCap;
        row.ChallengeCap = req.ChallengeCap;
        return row;
    }

    public void DeletePrimeConfig(string id)
    {
        var row = _primeConfigs.FirstOrDefault(x => x.Id == id);
        if (row is null) throw new KeyNotFoundException("Configuration introuvable");
        _primeConfigs.Remove(row);
    }

    private static string NewOrgNodeId(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    /// <summary>Crée un département sans pôle (ajoutez ensuite des pôles et cellules avant d’affecter un manager).</summary>
    public Department CreateOrgDepartment(string name)
    {
        var n = (name ?? "").Trim();
        if (n.Length == 0) throw new ArgumentException("Le nom du département est requis.");
        var dept = new Department { Id = NewOrgNodeId("d"), Name = n, Poles = [] };
        _departments.Add(dept);
        RebuildOrgDerivedLists();
        return dept;
    }

    public Pole CreateOrgPole(string departmentId, string name)
    {
        var dept = RequireDepartment(departmentId);
        var n = (name ?? "").Trim();
        if (n.Length == 0) throw new ArgumentException("Le nom du pôle est requis.");
        var pole = new Pole
        {
            Id = NewOrgNodeId("p"),
            Name = n,
            DepartmentId = dept.Id,
            Cells = [],
        };
        dept.Poles.Add(pole);
        RebuildOrgDerivedLists();
        return pole;
    }

    public Cellule CreateOrgCellule(string poleId, string name)
    {
        var (_, pole) = GetDepartmentForPole(poleId);
        var n = (name ?? "").Trim();
        if (n.Length == 0) throw new ArgumentException("Le nom de la cellule est requis.");
        var cellId = NewOrgNodeId("c");
        var teamId = NewOrgNodeId("t");
        var cell = new Cellule
        {
            Id = cellId,
            Name = n,
            PoleId = pole.Id,
            Teams = [new Team { Id = teamId, Name = "Équipe principale", CelluleId = cellId }],
        };
        pole.Cells.Add(cell);
        RebuildOrgDerivedLists();
        return cell;
    }

    private void RebuildOrgDerivedLists()
    {
        _etages.Clear();
        _etages.AddRange(_departments.Select(d => new EtageNode { Id = d.Id, Name = d.Name }));
        _services.Clear();
        _services.AddRange(_departments.SelectMany(d => d.Poles.Select(p => new ServiceNode { Id = p.Id, Name = p.Name, EtageId = d.Id })));
        _sousServices.Clear();
        _sousServices.AddRange(_departments.SelectMany(d =>
            d.Poles.SelectMany(p => p.Cells.Select(c => new SousServiceNode { Id = c.Id, Name = c.Name, ServiceId = p.Id }))));
    }

    /// <summary>Recharge départements / employés depuis PostgreSQL (après migrations + seed).</summary>
    public void HydrateOrganizationFromDatabase(PrimeDbContext db)
    {
        var rows = db.Departments
            .AsNoTracking()
            .Include(d => d.Poles).ThenInclude(p => p.Cells).ThenInclude(c => c.Teams)
            .OrderBy(d => d.Id)
            .ToList();
        if (rows.Count == 0)
            return;

        static List<Team> MapTeams(IEnumerable<TeamEntity> teams) =>
            teams.OrderBy(t => t.Id).Select(t => new Team { Id = t.Id, Name = t.Name, CelluleId = t.CelluleId }).ToList();

        static List<Cellule> MapCells(IEnumerable<CelluleEntity> cells) =>
            cells.OrderBy(c => c.Id).Select(c => new Cellule
            {
                Id = c.Id,
                Name = c.Name,
                PoleId = c.PoleId,
                Teams = MapTeams(c.Teams)
            }).ToList();

        static List<Pole> MapPoles(IEnumerable<PoleEntity> poles) =>
            poles.OrderBy(p => p.Id).Select(p => new Pole
            {
                Id = p.Id,
                Name = p.Name,
                DepartmentId = p.DepartmentId,
                Cells = MapCells(p.Cells)
            }).ToList();

        _departments.Clear();
        foreach (var d in rows)
        {
            _departments.Add(new Department
            {
                Id = d.Id,
                Name = d.Name,
                Poles = MapPoles(d.Poles)
            });
        }

        var empRows = db.Employees.AsNoTracking().OrderBy(e => e.Id).ToList();
        _employees.Clear();
        foreach (var e in empRows)
        {
            _employees.Add(new Employee
            {
                Id = e.Id,
                FirstName = e.FirstName,
                LastName = e.LastName,
                Role = e.Role,
                ParentId = e.ParentId,
                TeamId = e.TeamId,
                DepartementId = e.DepartementId,
                PoleId = e.PoleId,
                CelluleId = e.CelluleId,
                Email = e.Email,
                Avatar = e.Avatar
            });
        }

        RebuildOrgDerivedLists();
    }

    /// <summary>Vérifie si l’utilisateur a une affectation superviseur sur ce pôle (service).</summary>
    public bool SupervisorOwnsPole(string supervisorUserId, string poleId)
    {
        var u = supervisorUserId.Trim();
        var p = poleId.Trim();
        return _supervisorServiceAssignments.Any(a =>
            string.Equals(a.UserId, u, StringComparison.Ordinal) &&
            string.Equals(a.ServiceId, p, StringComparison.Ordinal));
    }

    public HashSet<string> GetSupervisedPoleIds(string supervisorUserId)
    {
        var u = supervisorUserId.Trim();
        return _supervisorServiceAssignments
            .Where(a => string.Equals(a.UserId, u, StringComparison.Ordinal))
            .Select(a => a.ServiceId)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Retourne l’identifiant du pôle contenant cette cellule, ou null.</summary>
    public string? GetPoleIdForCellule(string celluleId)
    {
        var key = celluleId.Trim();
        foreach (var d in _departments)
        {
            foreach (var pole in d.Poles)
            {
                if (pole.Cells.Any(c => string.Equals(c.Id, key, StringComparison.Ordinal)))
                    return pole.Id;
            }
        }

        return null;
    }
}
