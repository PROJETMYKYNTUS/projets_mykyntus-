export interface SubServiceSimple {
  id: number;
  name: string;
  serviceName: string;
}

export interface ServiceSimple {
  id: number;
  name: string;
  floorName: string;
}

/** Profil RH canonique (projection Directory / Planning). */
export interface UserHrProfile {
  dateNaissance?: string | null;
  villeNaissance?: string | null;
  nationalite?: string | null;
  sexe?: string | null;
  situationFamiliale?: string | null;
  nombreEnfants?: number | null;
  cin?: string | null;
  adresse?: string | null;
  telephone1?: string | null;
  telephoneUrgence?: string | null;
  relationUrgence?: string | null;
  rib?: string | null;
  immatriculationInterne?: string | null;
  immatriculationCnss?: string | null;
  dateEntree?: string | null;
  dateEmbauche?: string | null;
  dateAnciennete?: string | null;
  dateSortie?: string | null;
  dateEvolutionPoste?: string | null;
  ancienPoste?: string | null;
  ancienService?: string | null;
  niveauScolaire?: string | null;
  intitulesEtudes?: string | null;
  enFormation?: boolean;
  dateDebutFormation?: string | null;
  dateFinFormationPrevue?: string | null;
}

export interface User {
  id: number;
  guid: string;
  roleId: number;
  roleName: string;
  subServiceId?: number;
  subServiceName?: string;
  orgPoleName?: string | null;
  orgCelluleName?: string | null;
  orgServiceName?: string | null;
  orgOperationalDepartmentName?: string | null;
  managedSubServices: SubServiceSimple[];
  managedServices: ServiceSimple[];
  firstName: string;
  lastName: string;
  email: string;
  hireDate: string;
  isActive: boolean;
  createdAt: string;
  level: number;
  chefDeProjetId?: string | null;
  superviseurId?: string | null;
  referentTechniqueId?: string | null;
  hrProfile?: UserHrProfile | null;
  niveauExpertiseMetier?: number | null;
  customFields?: Record<string, string | null>;
}

export interface CreateUserDto {
  roleId: number;
  subServiceId?: number;
  managedSubServiceIds: number[];
  managedServiceIds: number[];
  firstName: string;
  lastName: string;
  hireDate: string;
  email: string;
  level: number;
  chefDeProjetId?: string | null;
  superviseurId?: string | null;
  referentTechniqueId?: string | null;
  hrProfile?: UserHrProfile | null;
  niveauExpertiseMetier?: number | null;
  customFields?: Record<string, string | null>;
}

export interface UpdateUserDto {
  roleId: number;
  subServiceId?: number;
  managedSubServiceIds: number[];
  managedServiceIds: number[];
  firstName: string;
  lastName: string;
  hireDate: string;
  email: string;
  isActive: boolean;
  level: number;
  chefDeProjetId?: string | null;
  superviseurId?: string | null;
  referentTechniqueId?: string | null;
  hrProfile?: UserHrProfile | null;
  niveauExpertiseMetier?: number | null;
  customFields?: Record<string, string | null>;
}
