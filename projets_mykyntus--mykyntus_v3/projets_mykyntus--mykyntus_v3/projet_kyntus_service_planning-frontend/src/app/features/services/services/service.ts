import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';
import { Service, ServiceDetail, CreateServiceDto, UpdateServiceDto } from '../services-module';
import { environment } from '../../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ServiceService {
  private apiUrl = `${environment.apiUrl}/services`;

  constructor(private http: HttpClient) {
    console.log('ServiceService - API URL:', this.apiUrl);
  }

  getAllServices(): Observable<Service[]> {
    return this.http.get<Service[]>(this.apiUrl).pipe(
      tap(data => console.log('Services reçus:', data)),
      catchError(err => {
        console.error('Erreur getAllServices:', err);
        return throwError(() => err);
      })
    );
  }

  getServicesByFloor(floorId: number): Observable<Service[]> {
    return this.http.get<Service[]>(`${this.apiUrl}/by-floor/${floorId}`).pipe(
      catchError(err => throwError(() => err))
    );
  }

  getServiceById(id: number): Observable<Service> {
    return this.http.get<Service>(`${this.apiUrl}/${id}`).pipe(
      catchError(err => throwError(() => err))
    );
  }

  getServiceDetails(id: number): Observable<ServiceDetail> {
    return this.http.get<ServiceDetail>(`${this.apiUrl}/${id}/details`).pipe(
      catchError(err => throwError(() => err))
    );
  }

  createService(dto: CreateServiceDto): Observable<Service> {
    return this.http.post<Service>(this.apiUrl, dto).pipe(
      catchError(err => throwError(() => err))
    );
  }

  updateService(id: number, dto: UpdateServiceDto): Observable<Service> {
    return this.http.put<Service>(`${this.apiUrl}/${id}`, dto).pipe(
      catchError(err => throwError(() => err))
    );
  }

  deleteService(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`).pipe(
      catchError(err => throwError(() => err))
    );
  }

  checkCodeUnique(code: string, excludeId?: number): Observable<{ isUnique: boolean }> {
    const params = excludeId ? `?excludeId=${excludeId}` : '';
    return this.http.get<{ isUnique: boolean }>(`${this.apiUrl}/check-code/${code}${params}`).pipe(
      catchError(err => throwError(() => err))
    );
  }
}