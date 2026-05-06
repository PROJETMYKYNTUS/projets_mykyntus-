import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';
import { User, CreateUserDto, UpdateUserDto } from '../users-module';
import { environment } from '../../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class UserService {
  private apiUrl = `${environment.apiUrl}/users`;

  constructor(private http: HttpClient) {}

  getAllUsers(): Observable<User[]> {
    return this.http.get<User[]>(this.apiUrl).pipe(
      tap(d => console.log('Users:', d)),
      catchError(err => throwError(() => err))
    );
  }

  getUsersBySubService(subServiceId: number): Observable<User[]> {
    return this.http.get<User[]>(`${this.apiUrl}/by-subservice/${subServiceId}`).pipe(
      catchError(err => throwError(() => err))
    );
  }

  getUserById(id: number): Observable<User> {
    return this.http.get<User>(`${this.apiUrl}/${id}`).pipe(
      catchError(err => throwError(() => err))
    );
  }

  createUser(dto: CreateUserDto): Observable<User> {
    return this.http.post<User>(this.apiUrl, dto).pipe(
      catchError(err => throwError(() => err))
    );
  }

  updateUser(id: number, dto: UpdateUserDto): Observable<User> {
    return this.http.put<User>(`${this.apiUrl}/${id}`, dto).pipe(
      catchError(err => throwError(() => err))
    );
  }

  deleteUser(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`).pipe(
      catchError(err => throwError(() => err))
    );
  }

  checkEmailUnique(email: string, excludeId?: number): Observable<{ isUnique: boolean }> {
    const params = excludeId ? `?excludeId=${excludeId}` : '';
    return this.http.get<{ isUnique: boolean }>(`${this.apiUrl}/check-email/${email}${params}`).pipe(
      catchError(err => throwError(() => err))
    );
  }
}