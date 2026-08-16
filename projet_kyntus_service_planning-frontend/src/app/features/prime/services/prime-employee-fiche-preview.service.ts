import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { parsePrimeSchemaFromDraftJson } from '../lib/prime-cell-schema-merge';
import {
  MERGED_PREVIEW_MISSING_SNAPSHOT_HINT,
  computeMergedEmployeeFichePreview,
  type MergedEmployeeFichePreviewResult,
} from '../lib/prime-employee-fiche-merged-preview';
import {
  buildStyledMergedFicheWorkbook,
  downloadStyledFicheWorkbook,
} from '../lib/prime-fiche-xlsx-export';
import type {
  CellulePrimeIndicatorDto,
  ServicePoleLinePonderationDto,
} from '../services/prime-cell-prime-api.service';
import { RoleService } from '../state/role.service';

const base = '/api/prime/fiches';

export interface MergedFichePreviewContextDto {
  ficheId: string;
  employeeId: string;
  employeeDisplayName: string;
  period: string;
  templateId: string;
  schemaJson: string;
  poleSaisieJson: string;
  cellSaisieJson: string;
  templateCalcSnapshotJson?: string | null;
  indicators: CellulePrimeIndicatorDto[];
  poleLinePonderations?: ServicePoleLinePonderationDto[];
  previewAvailable: boolean;
  previewUnavailableReason?: string | null;
}

export function previewHttpError(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    const b = err.error as { error?: string } | undefined;
    if (b?.error) return b.error;
    return err.message;
  }
  return err instanceof Error ? err.message : 'Erreur';
}

@Injectable({ providedIn: 'root' })
export class PrimeEmployeeFichePreviewService {
  private readonly http = inject(HttpClient);
  private readonly role = inject(RoleService);

  loadContext(ficheId: string): Observable<MergedFichePreviewContextDto> {
    const u = this.role.currentUser();
    const params = new HttpParams().set('userId', u.id).set('role', this.role.currentRole() as string);
    return this.http.get<MergedFichePreviewContextDto>(
      `${base}/${encodeURIComponent(ficheId)}/merged-preview-context`,
      { params },
    );
  }

  computePreview(context: MergedFichePreviewContextDto): MergedEmployeeFichePreviewResult {
    if (!context.previewAvailable) {
      return {
        rows: [],
        errors: [context.previewUnavailableReason ?? 'Aperçu indisponible.'],
        missingSnapshot: false,
        missingGridPositions: false,
        previewSheetName: null,
        effectiveSchema: null,
        parsedCell: null,
        totals: null,
      };
    }
    const schema = parsePrimeSchemaFromDraftJson(context.schemaJson);
    return computeMergedEmployeeFichePreview({
      schema,
      poleSaisieJson: context.poleSaisieJson,
      cellSaisieJson: context.cellSaisieJson,
      templateCalcSnapshotJson: context.templateCalcSnapshotJson,
      indicators: context.indicators,
      poleLinePonderations: context.poleLinePonderations ?? [],
      templateId: context.templateId,
    });
  }

  loadAndCompute(ficheId: string): Observable<{
    context: MergedFichePreviewContextDto;
    preview: MergedEmployeeFichePreviewResult;
  }> {
    return this.loadContext(ficheId).pipe(
      map((context) => ({
        context,
        preview: this.computePreview(context),
      })),
    );
  }

  async downloadXlsxFromContext(
    context: MergedFichePreviewContextDto,
    fileNameBase?: string,
  ): Promise<string | null> {
    if (!context.previewAvailable) {
      return context.previewUnavailableReason ?? 'Aperçu indisponible.';
    }
    const res = this.computePreview(context);
    if (res.missingSnapshot) return MERGED_PREVIEW_MISSING_SNAPSHOT_HINT;
    if (!res.rows.length) return res.errors[0] ?? 'Export impossible — grille vide.';
    if (!res.effectiveSchema) return 'Schéma indisponible : impossible de générer le livrable stylé.';
    const sheetName =
      (res.previewSheetName || 'Fiche_PRIME').replace(/[:\\/?*[\]]/g, '_').slice(0, 31) || 'Fiche_PRIME';
    const safe =
      (fileNameBase ?? `${context.employeeDisplayName}_${context.period}`)
        .replace(/[<>:"/\\|?*]+/g, '_')
        .trim() || 'fiche';
    const wb = await buildStyledMergedFicheWorkbook(res.rows, res.effectiveSchema, sheetName);
    await downloadStyledFicheWorkbook(wb, `PRIME_fiche_${safe}.xlsx`);
    return null;
  }
}
