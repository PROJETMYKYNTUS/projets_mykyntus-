import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AlertTriangle, Link2, RefreshCw, Search, Unlink, X } from 'lucide';
import { LucideIconComponent } from '../../../../shared/lucide-icon.component';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { formatHttpErrorMessage } from '../../../../core/lib/http-error-message.util';
import {
  HtelAmbiguousMatchDto,
  HtelApiService,
  HtelLiaisonsReportDto,
  HtelLinkedEmployeeDto,
  HtelOrphanTechnicienDto,
  HtelSyncReportDto,
  HtelUnlinkedEmployeeDto,
} from '../../services/htel-api.service';

@Component({
  selector: 'app-htel-liaisons-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LucideIconComponent, KyntusPageHeaderComponent],
  templateUrl: './htel-liaisons-page.component.html',
  styleUrls: ['./htel-liaisons-page.component.css'],
})
export class HtelLiaisonsPageComponent implements OnInit {
  readonly icons = {
    sync: RefreshCw,
    warn: AlertTriangle,
    link: Link2,
    unlink: Unlink,
    search: Search,
    clear: X,
  };

  private readonly htelApi = inject(HtelApiService);
  private readonly router = inject(Router);
  private readonly cdr = inject(ChangeDetectorRef);

  loading = false;
  syncing = false;
  error: string | null = null;
  syncReport: HtelSyncReportDto | null = null;
  report: HtelLiaisonsReportDto | null = null;
  searchTerm = '';

  linkEmployeeId = '';
  linkIdTechnicien: number | null = null;

  ngOnInit(): void {
    this.reload();
  }

  get linked(): HtelLinkedEmployeeDto[] {
    return this.report?.linked ?? [];
  }

  get orphans(): HtelOrphanTechnicienDto[] {
    return this.report?.orphansHtel ?? [];
  }

  get ambiguous(): HtelAmbiguousMatchDto[] {
    return this.report?.ambiguous ?? [];
  }

  get unlinked(): HtelUnlinkedEmployeeDto[] {
    return this.report?.unlinkedEmployees ?? [];
  }

  get filteredLinked(): HtelLinkedEmployeeDto[] {
    const q = this.normalize(this.searchTerm);
    if (!q) return this.linked;
    return this.linked.filter((r) =>
      this.matches(q, r.lastName, r.firstName, r.email, String(r.idTechnicien), r.htelCode, r.htelTechnicienName),
    );
  }

  get filteredOrphans(): HtelOrphanTechnicienDto[] {
    const q = this.normalize(this.searchTerm);
    if (!q) return this.orphans;
    return this.orphans.filter((r) =>
      this.matches(q, String(r.idTechnicien), r.technicien, r.code, r.actif === 1 ? 'oui' : 'non'),
    );
  }

  get filteredAmbiguous(): HtelAmbiguousMatchDto[] {
    const q = this.normalize(this.searchTerm);
    if (!q) return this.ambiguous;
    return this.ambiguous.filter(
      (r) =>
        this.matches(q, String(r.idTechnicien), r.technicien, r.code) ||
        r.candidates.some((c) => this.matches(q, c.lastName, c.firstName, c.email)),
    );
  }

  get filteredUnlinked(): HtelUnlinkedEmployeeDto[] {
    const q = this.normalize(this.searchTerm);
    if (!q) return this.unlinked;
    return this.unlinked.filter((r) => this.matches(q, r.lastName, r.firstName, r.email));
  }

  get hasSearch(): boolean {
    return this.normalize(this.searchTerm).length > 0;
  }

  clearSearch(): void {
    this.searchTerm = '';
    this.cdr.detectChanges();
  }

  reload(): void {
    this.loading = true;
    this.error = null;
    this.htelApi.getLiaisons().subscribe({
      next: (report) => {
        this.report = report;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.error = formatHttpErrorMessage(err, 'Impossible de charger les liaisons HTEL.');
        this.loading = false;
        this.cdr.detectChanges();
      },
    });
  }

  sync(): void {
    this.syncing = true;
    this.error = null;
    this.syncReport = null;
    this.htelApi.sync().subscribe({
      next: (report) => {
        this.syncReport = report;
        this.syncing = false;
        this.reload();
      },
      error: (err) => {
        this.error = formatHttpErrorMessage(err, 'Synchronisation HTEL impossible.');
        this.syncing = false;
        this.cdr.detectChanges();
      },
    });
  }

  unlink(row: HtelLinkedEmployeeDto): void {
    this.htelApi.unlink(row.employeeId).subscribe({
      next: () => this.reload(),
      error: (err) => {
        this.error = formatHttpErrorMessage(err, 'Impossible de délier.');
        this.cdr.detectChanges();
      },
    });
  }

  linkManual(): void {
    const employeeId = this.linkEmployeeId.trim();
    const idTechnicien = this.linkIdTechnicien;
    if (!employeeId || !idTechnicien || idTechnicien <= 0) {
      this.error = 'Sélectionnez un employé et un id technicien HTEL.';
      return;
    }
    this.htelApi.link(employeeId, idTechnicien).subscribe({
      next: () => {
        this.linkEmployeeId = '';
        this.linkIdTechnicien = null;
        this.reload();
      },
      error: (err) => {
        this.error = formatHttpErrorMessage(err, 'Liaison impossible.');
        this.cdr.detectChanges();
      },
    });
  }

  linkCandidate(employeeId: string, idTechnicien: number): void {
    this.htelApi.link(employeeId, idTechnicien).subscribe({
      next: () => this.reload(),
      error: (err) => {
        this.error = formatHttpErrorMessage(err, 'Liaison impossible.');
        this.cdr.detectChanges();
      },
    });
  }

  createEmployeeFromOrphan(orphan: HtelOrphanTechnicienDto): void {
    const parts = orphan.technicien.trim().split(/\s+/);
    const lastName = parts[0] ?? '';
    const firstName = parts.slice(1).join(' ');
    void this.router.navigate(['/users/create'], {
      queryParams: {
        lastName,
        firstName,
        idTechnicien: orphan.idTechnicien,
      },
    });
  }

  private normalize(value: string | null | undefined): string {
    return (value ?? '')
      .trim()
      .normalize('NFD')
      .replace(/\p{M}/gu, '')
      .toLowerCase();
  }

  private matches(query: string, ...parts: Array<string | null | undefined>): boolean {
    const haystack = this.normalize(parts.filter(Boolean).join(' '));
    return haystack.includes(query);
  }
}
