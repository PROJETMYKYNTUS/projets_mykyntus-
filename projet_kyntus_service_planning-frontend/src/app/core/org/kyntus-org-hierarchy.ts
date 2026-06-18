/**
 * Shared org hierarchy drill-down helpers for Prime, Documentation and Parrainage.
 * Re-exports from feature modules until callers migrate to this barrel.
 */
export type { HierarchyDrillSelection } from '../../features/prime/lib/hierarchyDrillDown';

export {
  listChildrenByRole,
  listManagersUnderRp,
  listCoachesUnderManager,
  listPilotesUnderCoach,
  piloteIdsForManagerDrill,
  piloteIdsForRpDrill,
  applyDrillDownToEmployeeRows,
  drillSelectOptions,
} from '../../features/prime/lib/hierarchyDrillDown';

export type { HierarchyDrillSelection as DocumentationDrillSelection } from '../../features/documentation/lib/documentation-org-hierarchy';

export {
  displayDirectoryUserLabel,
  listManagersUnderRp as listDocManagersUnderRp,
  listCoachesUnderManager as listDocCoachesUnderManager,
  listPilotesUnderCoach as listDocPilotesUnderCoach,
  drillSelectOptions as docDrillSelectOptions,
  visibleEmployeeIdsForRole,
} from '../../features/documentation/lib/documentation-org-hierarchy';

export type { HierarchyDrillSelection as ParrainageDrillSelection } from '../../features/parrainage/lib/org-hierarchy';

export {
  listManagersUnderRp as listParrainageManagersUnderRp,
  listCoachesUnderManager as listParrainageCoachesUnderManager,
  piloteIdsForManagerDrill as parrainagePiloteIdsForManagerDrill,
  piloteIdsForRpDrill as parrainagePiloteIdsForRpDrill,
} from '../../features/parrainage/lib/org-hierarchy';
