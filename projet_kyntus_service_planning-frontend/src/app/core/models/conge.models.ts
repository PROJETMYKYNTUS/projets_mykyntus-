export enum TypeConge {
  Annuel       = 1,
  Exceptionnel = 2,
  Maternite    = 3,
  Paternite    = 4,
  Maladie      = 5
}

export enum StatutDemande {
  EnAttente = 1,
  Validee   = 2,
  Refusee   = 3,
  Annulee   = 4
}

export enum TypeCongeExceptionnel {
  Mariage       = 1,
  DecesConjoint = 2,
  DecesParent   = 3,
  Naissance     = 4,
  Maternite     = 5
}

export const TypeCongeLabels: Record<TypeConge, string> = {
  [TypeConge.Annuel]:       'Congé annuel',
  [TypeConge.Exceptionnel]: 'Congé exceptionnel',
  [TypeConge.Maternite]:    'Congé maternité',
  [TypeConge.Paternite]:    'Congé paternité',
  [TypeConge.Maladie]:      'Congé maladie'
};

export const StatutDemandeLabels: Record<StatutDemande, string> = {
  [StatutDemande.EnAttente]: 'En attente',
  [StatutDemande.Validee]:   'Validée',
  [StatutDemande.Refusee]:   'Refusée',
  [StatutDemande.Annulee]:   'Annulée'
};

export const TypeCongeExceptionnelLabels: Record<TypeCongeExceptionnel, string> = {
  [TypeCongeExceptionnel.Mariage]:       'Mariage (4 jours)',
  [TypeCongeExceptionnel.DecesConjoint]: 'Décès conjoint (3 jours)',
  [TypeCongeExceptionnel.DecesParent]:   'Décès parent (2 jours)',
  [TypeCongeExceptionnel.Naissance]:     'Naissance (3 jours)',
  [TypeCongeExceptionnel.Maternite]:     'Maternité (98 jours)'
};

export interface DemandeCongeDto {
  id:                 string;
  employeId:          string;
  managerId:          string;
  typeConge:          TypeConge;
  typeExceptionnel:   TypeCongeExceptionnel | null;
  dateDebut:          string;
  dateFin:            string;
  nombreJours:        number;
  statut:             StatutDemande;
  motif:              string | null;
  commentaireManager: string | null;
  dateDemande:        string;
  dateDecision:       string | null;
  nomEmploye?: string;
prenomEmploye?: string;
}

export interface SoldeCongeDto {
  employeId:    string;
  annee:        number;
  soldeInitial: number;
  soldeUtilise: number;
  soldeRestant: number;
}

export interface DemanderCongeCommand {
  employeId:        string;
  typeConge:        TypeConge;
  dateDebut:        string;
    dateFin: string | null; 
  motif:            string | null;
  typeExceptionnel: TypeCongeExceptionnel | null;
}

export interface ValiderCongeRequest {
  managerId:   string;
  commentaire: string | null;
}

export interface RefuserCongeRequest {
  managerId:   string;
  commentaire: string;
}