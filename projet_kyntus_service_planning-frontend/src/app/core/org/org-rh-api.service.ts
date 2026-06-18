/**
 * API Organisation RH plateforme (gateway → /api/prime/org/*).
 * Réexporte le service existant en attendant migration complète des imports.
 */
export {
  PrimeOrgApiService as OrgRhApiService,
  type OrgAssignmentsOverview,
  type EnsureEmployeeFromPlanningDto,
  type EtageNodeDto,
  type ServiceNodeDto,
  type SousServiceNodeDto,
  type ManagerEtageAssignmentDto,
  type SupervisorServiceAssignmentDto,
  type CoachSousServiceAssignmentDto,
} from '../../features/prime/services/prime-org-api.service';
