import type { Employee, PrimeResult } from '../models';
import type {
  EmployeePrimeServiceFicheValidationDto,
  PrimeFicheValidationStatus,
} from '../services/prime-fiche-result.service';

/** Données agrégées `/api/prime/results` → même shape que l’API validation fiches. */
export function mapPrimeResultToFicheDto(
  r: PrimeResult,
  employees: Employee[],
): EmployeePrimeServiceFicheValidationDto {
  const emp = employees.find((e) => e.id === r.employeeId);
  return {
    id: r.id,
    employeeId: r.employeeId,
    supervisorUserId: emp?.parentId ?? '',
    serviceId: emp?.serviceId ?? '—',
    celluleId: emp?.celluleId ?? '—',
    period: r.period,
    fillingStatus: '—',
    validationStatus: r.status as PrimeFicheValidationStatus,
    lastApproverUserId: r.approvedBy ?? null,
    lastApprovedAt: r.date ? `${r.date}T12:00:00.000Z` : null,
    rejectedByUserId: null,
    rejectedAt: null,
    rejectionReason: null,
    primeAmount: r.amount,
    challengeAmount: null,
    totalAmount: r.score,
    updatedAt: r.date ? `${r.date}T12:00:00.000Z` : new Date().toISOString(),
  };
}
