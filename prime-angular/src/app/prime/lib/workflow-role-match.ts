/** Aligné sur PrimeRbacReadService.RolesMatchWorkflowApprover côté API. */
export function rolesMatchWorkflowApprover(employeeRole: string, stepApproverRole: string): boolean {
  if (employeeRole === stepApproverRole) return true;
  if (employeeRole === 'RP' && stepApproverRole === 'Chef de projet') return true;
  if (employeeRole === 'Coach' && stepApproverRole === 'Référent technique') return true;
  if (employeeRole === 'Référent technique' && stepApproverRole === 'Coach') return true;
  if (employeeRole === 'Comptable' && stepApproverRole === 'Comptabilité') return true;
  if (employeeRole === 'Comptabilité' && stepApproverRole === 'Comptable') return true;
  return false;
}
