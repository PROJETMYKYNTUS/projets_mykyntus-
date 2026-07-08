import type { User, UserHrProfile } from '../../features/users/users-module';
import { orgPerimeterSummary, type UserOrgPerimeterView } from '../org/user-org-perimeter';
import {
  HR_EDUCATION_LEVEL_OPTIONS,
  maritalStatusLabel,
  nationalityLabelForCode,
} from './hr-form-options';

export function contractLevelLabel(level: number): string {
  if (level === 2) return 'Intermédiaire';
  if (level === 3) return 'Confirmé';
  return 'Débutant';
}

export function expertiseLevelLabel(level: number | null | undefined): string | null {
  if (level == null || level < 1 || level > 3) return null;
  return contractLevelLabel(level);
}

export function hrDisplayValue(value: string | null | undefined): string {
  return value?.trim() ? value.trim() : '—';
}

export function nationaliteDisplay(codeOrLabel: string | null | undefined): string {
  if (!codeOrLabel?.trim()) return '—';
  const trimmed = codeOrLabel.trim();
  const fromCode = nationalityLabelForCode(trimmed);
  return fromCode || trimmed;
}

export function niveauScolaireDisplay(codeOrLabel: string | null | undefined): string {
  if (!codeOrLabel?.trim()) return '—';
  const trimmed = codeOrLabel.trim();
  const known = HR_EDUCATION_LEVEL_OPTIONS.find((o) => o.value === trimmed);
  return known?.label ?? trimmed;
}

export function situationFamilialeDisplay(code: string | null | undefined): string {
  if (!code?.trim()) return '—';
  return maritalStatusLabel(code.trim());
}

export function sexeDisplay(sexe: string | null | undefined): string {
  if (!sexe?.trim()) return '—';
  const s = sexe.trim().toUpperCase();
  if (s === 'M') return 'Homme';
  if (s === 'F') return 'Femme';
  return sexe.trim();
}

export function boolDisplay(value: boolean | null | undefined): string {
  if (value == null) return '—';
  return value ? 'Oui' : 'Non';
}

export function dateDisplay(value: string | null | undefined): string {
  if (!value?.trim()) return '—';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value.trim();
  return d.toLocaleDateString('fr-FR');
}

/** Date de référence pour l'ancienneté (profil RH puis embauche). */
export function seniorityReferenceDate(user: User): string {
  return user.hrProfile?.dateAnciennete?.trim()
    || user.hrProfile?.dateEntree?.trim()
    || user.hireDate;
}

export function matriculeDisplay(user: User): string {
  return hrDisplayValue(user.hrProfile?.immatriculationInterne);
}

export function telephoneDisplay(user: User): string {
  return hrDisplayValue(user.hrProfile?.telephone1);
}

export interface HrProfileDisplayRow {
  label: string;
  value: string;
}

export interface EmployeeDetailSection {
  id: string;
  title: string;
  description?: string;
  rows: HrProfileDisplayRow[];
}

export function buildHrProfileDisplayRows(profile: UserHrProfile | null | undefined): HrProfileDisplayRow[] {
  return buildCompleteHrProfileDisplayRows(profile).filter((r) => r.value !== '—');
}

/** Tous les champs RH, y compris vides (—). */
export function buildCompleteHrProfileDisplayRows(profile: UserHrProfile | null | undefined): HrProfileDisplayRow[] {
  const p = profile ?? {};
  return [
    { label: 'Date de naissance', value: dateDisplay(p.dateNaissance) },
    { label: 'Ville de naissance', value: hrDisplayValue(p.villeNaissance) },
    { label: 'Nationalité', value: nationaliteDisplay(p.nationalite) },
    { label: 'Sexe', value: sexeDisplay(p.sexe) },
    { label: 'Situation familiale', value: situationFamilialeDisplay(p.situationFamiliale) },
    {
      label: "Nombre d'enfants",
      value: p.nombreEnfants != null ? String(p.nombreEnfants) : '—',
    },
    { label: 'CIN', value: hrDisplayValue(p.cin) },
    { label: 'Adresse', value: hrDisplayValue(p.adresse) },
    { label: 'Téléphone', value: hrDisplayValue(p.telephone1) },
    { label: 'Téléphone urgence', value: hrDisplayValue(p.telephoneUrgence) },
    { label: 'Relation urgence', value: hrDisplayValue(p.relationUrgence) },
    { label: 'RIB', value: hrDisplayValue(p.rib) },
    { label: 'Matricule interne', value: hrDisplayValue(p.immatriculationInterne) },
    { label: 'Immatriculation CNSS', value: hrDisplayValue(p.immatriculationCnss) },
    { label: "Date d'entrée", value: dateDisplay(p.dateEntree) },
    { label: "Date d'embauche (RH)", value: dateDisplay(p.dateEmbauche) },
    { label: "Date d'ancienneté", value: dateDisplay(p.dateAnciennete) },
    { label: 'Date de sortie', value: dateDisplay(p.dateSortie) },
    { label: 'Date évolution poste', value: dateDisplay(p.dateEvolutionPoste) },
    { label: 'Ancien poste', value: hrDisplayValue(p.ancienPoste) },
    { label: 'Ancien service', value: hrDisplayValue(p.ancienService) },
    { label: 'Niveau scolaire', value: niveauScolaireDisplay(p.niveauScolaire) },
    { label: 'Intitulés études', value: hrDisplayValue(p.intitulesEtudes) },
    { label: 'En formation', value: boolDisplay(p.enFormation) },
    { label: 'Début formation', value: dateDisplay(p.dateDebutFormation) },
    { label: 'Fin formation prévue', value: dateDisplay(p.dateFinFormationPrevue) },
  ];
}

export function orgSummaryForList(_user: User, perimeter: UserOrgPerimeterView): string {
  return orgPerimeterSummary(perimeter);
}

export function resolveEmployeeName(
  employees: readonly { id: string; firstName: string; lastName: string }[],
  guid: string | null | undefined,
): string {
  if (!guid?.trim()) return '—';
  const id = guid.trim().toLowerCase();
  const emp = employees.find((e) => e.id.trim().toLowerCase() === id);
  return emp ? `${emp.firstName} ${emp.lastName}`.trim() : guid;
}

export function buildEmployeeDetailSections(
  user: User,
  options: {
    mentorEmployees?: readonly { id: string; firstName: string; lastName: string }[];
    contractRows?: HrProfileDisplayRow[];
    customFieldRows?: HrProfileDisplayRow[];
  } = {},
): EmployeeDetailSection[] {
  const mentors = options.mentorEmployees ?? [];
  const hr = user.hrProfile;
  const allHr = buildCompleteHrProfileDisplayRows(hr);

  const sections: EmployeeDetailSection[] = [
    {
      id: 'account',
      title: 'Compte & accès',
      description: 'Identifiants et statut du compte employé.',
      rows: [
        { label: 'Identifiant Planning', value: String(user.id) },
        { label: 'GUID employé', value: hrDisplayValue(user.guid) },
        { label: 'Email', value: hrDisplayValue(user.email) },
        { label: 'Rôle', value: hrDisplayValue(user.roleName) },
        { label: 'Statut compte', value: user.isActive ? 'Actif' : 'Inactif' },
        { label: 'Compte créé le', value: dateDisplay(user.createdAt) },
        { label: 'Niveau contractuel', value: `${contractLevelLabel(user.level)} (N${user.level})` },
        {
          label: 'Niveau expertise métier',
          value: user.niveauExpertiseMetier
            ? `${expertiseLevelLabel(user.niveauExpertiseMetier) ?? '—'} (E${user.niveauExpertiseMetier})`
            : '—',
        },
        { label: 'Sous-service (ID)', value: user.subServiceId != null ? String(user.subServiceId) : '—' },
        { label: 'Sous-service (libellé)', value: hrDisplayValue(user.subServiceName) },
      ],
    },
    {
      id: 'mentors',
      title: 'Responsables hiérarchiques',
      description: 'Encadrement explicite renseigné sur la fiche employé.',
      rows: [
        {
          label: 'Chef de projet',
          value: resolveEmployeeName(mentors, user.chefDeProjetId),
        },
        {
          label: 'Superviseur',
          value: resolveEmployeeName(mentors, user.superviseurId),
        },
        {
          label: 'Référent technique',
          value: resolveEmployeeName(mentors, user.referentTechniqueId),
        },
      ],
    },
    {
      id: 'identity',
      title: 'Identité & état civil',
      rows: allHr.filter((r) =>
        ['Date de naissance', 'Ville de naissance', 'Nationalité', 'Sexe', 'Situation familiale', "Nombre d'enfants", 'CIN'].includes(r.label),
      ),
    },
    {
      id: 'contact',
      title: 'Coordonnées & administratif',
      rows: allHr.filter((r) =>
        ['Adresse', 'Téléphone', 'Téléphone urgence', 'Relation urgence', 'RIB', 'Matricule interne', 'Immatriculation CNSS'].includes(r.label),
      ),
    },
    {
      id: 'career',
      title: 'Carrière & dates',
      rows: [
        { label: "Date d'embauche (compte)", value: dateDisplay(user.hireDate) },
        ...allHr.filter((r) =>
          ["Date d'entrée", "Date d'embauche (RH)", "Date d'ancienneté", 'Date de sortie', 'Date évolution poste', 'Ancien poste', 'Ancien service'].includes(r.label),
        ),
      ],
    },
    {
      id: 'education',
      title: 'Formation & parcours scolaire',
      rows: allHr.filter((r) =>
        ['Niveau scolaire', 'Intitulés études', 'En formation', 'Début formation', 'Fin formation prévue'].includes(r.label),
      ),
    },
  ];

  if (options.contractRows !== undefined) {
    sections.push({
      id: 'contract',
      title: 'Contrat',
      description: 'Contrat(s) enregistré(s) pour cet employé.',
      rows: options.contractRows.length
        ? options.contractRows
        : [{ label: 'Contrat', value: 'Aucun contrat enregistré' }],
    });
  }

  if (options.customFieldRows !== undefined) {
    sections.push({
      id: 'custom',
      title: 'Champs personnalisés',
      rows: options.customFieldRows.length
        ? options.customFieldRows
        : [{ label: 'Configuration', value: 'Aucun champ personnalisé configuré' }],
    });
  }

  if (user.managedServices?.length || user.managedSubServices?.length) {
    sections.push({
      id: 'supervised',
      title: 'Périmètre supervisé (charges structure)',
      rows: [
        ...user.managedServices.map((s, i) => ({
          label: `Service géré ${i + 1}`,
          value: `${s.floorName} / ${s.name}`,
        })),
        ...user.managedSubServices.map((s, i) => ({
          label: `Équipe gérée ${i + 1}`,
          value: `${s.serviceName} / ${s.name}`,
        })),
      ],
    });
  }

  return sections;
}

export function buildContractDisplayRows(contract: {
  type: string;
  status: string;
  startDate: string;
  endDate?: string;
  probationEndDate?: string;
  alertThresholdDays: number;
  notes?: string;
  joursRestants?: number;
  joursRestantsPeriodeEssai?: number;
}): HrProfileDisplayRow[] {
  return [
    { label: 'Type de contrat', value: hrDisplayValue(contract.type) },
    { label: 'Statut', value: hrDisplayValue(contract.status) },
    { label: 'Date de début', value: dateDisplay(contract.startDate) },
    { label: 'Date de fin', value: dateDisplay(contract.endDate) },
    { label: 'Fin période d\'essai', value: dateDisplay(contract.probationEndDate) },
    { label: 'Jours restants contrat', value: contract.joursRestants != null ? String(contract.joursRestants) : '—' },
    { label: 'Jours restants essai', value: contract.joursRestantsPeriodeEssai != null ? String(contract.joursRestantsPeriodeEssai) : '—' },
    { label: 'Seuil alerte (jours)', value: String(contract.alertThresholdDays) },
    { label: 'Notes', value: hrDisplayValue(contract.notes) },
  ];
}

export function userMatchesSearch(user: User, term: string): boolean {
  const haystack = [
    user.firstName,
    user.lastName,
    user.email,
    user.roleName,
    user.hrProfile?.immatriculationInterne,
    user.hrProfile?.cin,
    user.hrProfile?.telephone1,
    user.orgPoleName,
    user.orgCelluleName,
    user.orgServiceName,
    user.orgOperationalDepartmentName,
  ]
    .filter(Boolean)
    .join(' ')
    .toLowerCase();
  return haystack.includes(term);
}
