import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import type {
  InitialTrainingPathDto,
  TrainingSessionDto,
} from '../models/formation-training.models';

const PREFIX = '/api/formations';

@Injectable({ providedIn: 'root' })
export class FormationTrainingService {
  private readonly http = inject(HttpClient);

  listSessions(): Promise<TrainingSessionDto[]> {
    return firstValueFrom(this.http.get<TrainingSessionDto[]>(`${PREFIX}/sessions`));
  }

  listMyAnimatedSessions(animatorUserId: string): Promise<TrainingSessionDto[]> {
    return firstValueFrom(
      this.http.get<TrainingSessionDto[]>(`${PREFIX}/sessions/my-animated`, {
        params: { animatorUserId },
      }),
    );
  }

  createSession(body: Record<string, unknown>): Promise<TrainingSessionDto> {
    return firstValueFrom(this.http.post<TrainingSessionDto>(`${PREFIX}/sessions`, body)).catch((err) => {
      const msg =
        err?.error?.error ||
        err?.error?.title ||
        (typeof err?.error === 'string' ? err.error : null) ||
        err?.message ||
        'Échec de la création';
      throw new Error(msg);
    });
  }

  assignEmployees(sessionId: string, employees: { employeeId: string; employeeName: string }[]): Promise<unknown> {
    return firstValueFrom(
      this.http.post(`${PREFIX}/sessions/${sessionId}/assign`, { employees }),
    );
  }

  patchSessionStatus(sessionId: string, status: string): Promise<TrainingSessionDto> {
    return firstValueFrom(
      this.http.patch<TrainingSessionDto>(`${PREFIX}/sessions/${sessionId}`, { status }),
    );
  }

  createInitialPath(body: {
    employeeId: string;
    employeeName: string;
    dateDebut: string;
    dateFinPrevue: string;
  }): Promise<InitialTrainingPathDto> {
    return firstValueFrom(this.http.post<InitialTrainingPathDto>(`${PREFIX}/initial-paths`, body));
  }

  listFormateurInitial(): Promise<InitialTrainingPathDto[]> {
    return firstValueFrom(this.http.get<InitialTrainingPathDto[]>(`${PREFIX}/initial-paths/formateur`));
  }

  listRhPendingInitial(): Promise<InitialTrainingPathDto[]> {
    return firstValueFrom(this.http.get<InitialTrainingPathDto[]>(`${PREFIX}/initial-paths/rh-pending`));
  }

  listInitialOverview(): Promise<InitialTrainingPathDto[]> {
    return firstValueFrom(this.http.get<InitialTrainingPathDto[]>(`${PREFIX}/initial-paths/overview`));
  }

  listInitialByEmployee(employeeId: string): Promise<InitialTrainingPathDto[]> {
    return firstValueFrom(
      this.http.get<InitialTrainingPathDto[]>(`${PREFIX}/initial-paths/by-employee/${employeeId}`),
    );
  }

  recordQuiz(pathId: string, body: { quizScore: number; quizPassed: boolean; formateurComment?: string; recordedBy: string }): Promise<InitialTrainingPathDto> {
    return firstValueFrom(this.http.post<InitialTrainingPathDto>(`${PREFIX}/initial-paths/${pathId}/quiz-result`, body));
  }

  formateurValidate(pathId: string): Promise<InitialTrainingPathDto> {
    return firstValueFrom(this.http.post<InitialTrainingPathDto>(`${PREFIX}/initial-paths/${pathId}/formateur-validate`, {}));
  }

  formateurReject(pathId: string, body: { rejectedBy: string; reason: string }): Promise<InitialTrainingPathDto> {
    return firstValueFrom(this.http.post<InitialTrainingPathDto>(`${PREFIX}/initial-paths/${pathId}/formateur-reject`, body));
  }

  extendInitial(pathId: string, dateFinPrevue: string): Promise<InitialTrainingPathDto> {
    return firstValueFrom(
      this.http.post<InitialTrainingPathDto>(`${PREFIX}/initial-paths/${pathId}/extend`, { dateFinPrevue }),
    );
  }

  rhValidate(pathId: string): Promise<InitialTrainingPathDto> {
    return firstValueFrom(this.http.post<InitialTrainingPathDto>(`${PREFIX}/initial-paths/${pathId}/rh-validate`, {}));
  }

  rhReject(pathId: string, body: { rejectedBy: string; reason: string }): Promise<InitialTrainingPathDto> {
    return firstValueFrom(this.http.post<InitialTrainingPathDto>(`${PREFIX}/initial-paths/${pathId}/rh-reject`, body));
  }
}
