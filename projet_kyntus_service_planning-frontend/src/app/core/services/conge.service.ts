import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import {
  DemandeCongeDto, SoldeCongeDto, DemanderCongeCommand,
  ValiderCongeRequest, RefuserCongeRequest, StatutDemande
} from '../models/conge.models';

@Injectable({ providedIn: 'root' })
export class CongeService {
  private readonly base = '/api/conges';

  constructor(private http: HttpClient) {}

  getDemandesByEmploye(employeId: string, statut?: StatutDemande): Observable<DemandeCongeDto[]> {
    const params = statut !== undefined ? `?statut=${statut}` : '';
    return this.http.get<DemandeCongeDto[]>(`${this.base}/employe/${employeId}${params}`);
  }

  getSolde(employeId: string, annee?: number): Observable<SoldeCongeDto> {
    const params = annee !== undefined ? `?annee=${annee}` : '';
    return this.http.get<SoldeCongeDto>(`${this.base}/employe/${employeId}/solde${params}`);
  }

  getHistorique(employeId: string, annee: number): Observable<DemandeCongeDto[]> {
    return this.http.get<DemandeCongeDto[]>(
      `${this.base}/employe/${employeId}/historique?annee=${annee}`
    );
  }

  getHistoriqueRh(annee: number): Observable<DemandeCongeDto[]> {
    return this.http.get<DemandeCongeDto[]>(`${this.base}/historique?annee=${annee}`);
  }

  /** Compteur léger pour le tableau de bord RH (évite de charger tout l'historique). */
  getPendingRhCount(): Observable<number> {
    return this.http.get<{ count: number }>(`${this.base}/rh/pending-count`).pipe(
      map((r) => r.count ?? 0),
    );
  }

  demanderConge(cmd: DemanderCongeCommand): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.base, cmd);
  }

  annulerConge(demandeId: string, employeId: string): Observable<void> {
    return this.http.put<void>(`${this.base}/${demandeId}/annuler?employeId=${employeId}`, {});
  }

  getDemandesByManager(managerId: string): Observable<DemandeCongeDto[]> {
    return this.http.get<DemandeCongeDto[]>(`${this.base}/manager/${managerId}`);
  }

  validerConge(demandeId: string, req: ValiderCongeRequest): Observable<void> {
    return this.http.put<void>(`${this.base}/${demandeId}/valider`, req);
  }

  refuserConge(demandeId: string, req: RefuserCongeRequest): Observable<void> {
    return this.http.put<void>(`${this.base}/${demandeId}/refuser`, req);
  }
}