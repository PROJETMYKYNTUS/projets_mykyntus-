// src/app/core/models/reclamation.model.ts

export type ReclamationStatus = 'Soumise' | 'EnCours' | 'Traitee' | 'Rejetee' | 'Cloturee';
export type PropositionStatus  = 'Soumise' | 'EnEvaluation' | 'Approuvee' | 'Rejetee' | 'EnCours' | 'Implementee';
export type Priority           = 'Basse' | 'Normale' | 'Haute' | 'Critique';
export type ReclamationType    = 'ServiceQualite' | 'RessourcesHumaines' | 'Technique' | 'Administrative' | 'Autre';

export interface Reclamation {
  id: number;
  titre: string;
  description: string;
  type: ReclamationType;
  status: ReclamationStatus;
  priorite: Priority;
  auteurId: string;
  auteurNom: string;
  auteurRole: string;
  assigneeId?: string;
  assigneeNom?: string;
  commentaireTraitement?: string;
  satisfactionNote?: number;
  satisfactionCommentaire?: string;
  createdAt: string;
  updatedAt: string;
  traiteeAt?: string;
  clotureeAt?: string;
}

export interface ReclamationDetail extends Reclamation {
  historique?: HistoriqueItem[];

}

export interface Proposition {
  id: number;
  titre: string;
  description: string;
  beneficeAttendu?: string;
  status: PropositionStatus;
  priorite: Priority;
  auteurId: string;
  auteurNom: string;
  auteurRole: string;
  assigneeId?: string;
  assigneeNom?: string;
  commentaireEvaluation?: string;
  satisfactionNote?: number;
  satisfactionCommentaire?: string;
  createdAt: string;
  updatedAt: string;
  evalueeAt?: string;
  implementeeAt?: string;
}

export interface PropositionDetail extends Proposition {
  historique?: HistoriqueItem[];
}

export interface HistoriqueItem {
  id: number;
  action: string;
  valeur: string;
  effectueParNom: string;
  createdAt: string;
}

export interface PaginatedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface SatisfactionReport {
  moyenneNote: number;
  totalDemandes: number;
  totalAvecNote: number;
  repartitionNotes: Record<number, number>;
  parStatut: Record<string, number>;
  parPriorite: Record<string, number>;
}

// ── Payloads requêtes ──
export interface CreateReclamationPayload {
  titre: string;
  description: string;
  type: ReclamationType;
}

export interface CreatePropositionPayload {
  titre: string;
  description: string;
  beneficeAttendu?: string;
}

export interface UpdateStatusPayload {
  status: string;
  commentaire?: string;
}

export interface AssignPayload {
  assigneeId: string;
  assigneeNom: string;
}

export interface PrioriserPayload {
  priorite: Priority;
}

export interface SatisfactionPayload {
  note: number;
  commentaire?: string;
}