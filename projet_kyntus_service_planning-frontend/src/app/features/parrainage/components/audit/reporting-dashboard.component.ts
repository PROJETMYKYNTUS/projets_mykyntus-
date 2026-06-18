import { ChangeDetectionStrategy, Component } from '@angular/core';
import { Download, FileSpreadsheet, FileText } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import {
  ACTIONS_BY_ROLE,
  ACTIONS_BY_TYPE,
  ACTIVITY_BY_DAY,
  REPORTING_KPIS,
  TOP_ACTIVE_USERS,
} from '../../audit/audit-demo-data';

const toCsv = (headers: string[], data: Array<Array<string | number>>) =>
  [headers.join(';'), ...data.map((d) => d.join(';'))].join('\n');

const download = (name: string, content: string, mime: string) => {
  const blob = new Blob([content], { type: mime });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = name;
  a.click();
  URL.revokeObjectURL(url);
};

@Component({
  selector: 'app-reporting-dashboard',
  standalone: true,
  imports: [LucideIconComponent],
  template: `
    <div class="space-y-6">
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
        <div class="card-navy p-4 border border-blue-500/20 hover:border-blue-500/40 transition-colors">
          <p class="text-[11px] uppercase tracking-wide text-muted">Total actions</p>
          <p class="text-2xl font-bold text-white mt-1">{{ kpis.totalActions.toLocaleString('fr-FR') }}</p>
        </div>
        <div class="card-navy p-4 border border-rose-500/20 hover:border-rose-500/40 transition-colors">
          <p class="text-[11px] uppercase tracking-wide text-muted">% actions critiques</p>
          <p class="text-2xl font-bold text-rose-300 mt-1">{{ kpis.criticalPercent }}%</p>
        </div>
        <div class="card-navy p-4 border border-emerald-500/20 hover:border-emerald-500/40 transition-colors">
          <p class="text-[11px] uppercase tracking-wide text-muted">Utilisateurs actifs</p>
          <p class="text-2xl font-bold text-emerald-200 mt-1">{{ kpis.activeUsers }}</p>
        </div>
        <div class="card-navy p-4 border border-amber-500/20 hover:border-amber-500/40 transition-colors">
          <p class="text-[11px] uppercase tracking-wide text-muted">Anomalies détectées</p>
          <p class="text-2xl font-bold text-amber-200 mt-1">{{ kpis.anomaliesCount }}</p>
        </div>
      </div>

      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
        <div class="card-navy p-4 border border-slate-600/30">
          <p class="text-[11px] uppercase tracking-wide text-muted">Actions / jour (moy.)</p>
          <p class="text-2xl font-bold text-white mt-1">{{ kpis.actionsPerDay }}</p>
        </div>
        <div class="card-navy p-4 border border-slate-600/30 sm:col-span-2">
          <p class="text-[11px] uppercase tracking-wide text-muted">Utilisateur le plus actif</p>
          <p class="text-lg font-semibold text-emerald-200 mt-1 truncate">{{ kpis.topUser }}</p>
          <p class="text-xs text-muted">{{ kpis.topUserActions }} actions</p>
        </div>
        <div class="card-navy p-4 border border-violet-500/20 flex flex-col justify-center gap-2">
          <button type="button" (click)="exportPdfHtml()" class="inline-flex items-center justify-center gap-2 px-3 py-2 rounded-lg bg-violet-600/80 hover:bg-violet-500 text-white text-sm transition-colors">
            <app-lucide-icon [icon]="fileTextIcon" className="w-4 h-4" />
            Rapport PDF (HTML)
          </button>
          <button type="button" (click)="print()" class="inline-flex items-center justify-center gap-2 px-3 py-2 rounded-lg border border-default text-primary text-sm hover:bg-input transition-colors">
            <app-lucide-icon [icon]="downloadIcon" className="w-4 h-4" />
            Imprimer / PDF
          </button>
          <button type="button" (click)="exportExcel()" class="inline-flex items-center justify-center gap-2 px-3 py-2 rounded-lg border border-emerald-600/50 text-emerald-200 text-sm hover:bg-emerald-950/40 transition-colors">
            <app-lucide-icon [icon]="sheetIcon" className="w-4 h-4" />
            Excel (synthèse)
          </button>
          <button type="button" (click)="exportMonthlyCsv()" class="inline-flex items-center justify-center gap-2 px-3 py-2 rounded-lg border border-default text-muted text-xs hover:bg-input transition-colors">
            Export CSV activité
          </button>
        </div>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <div class="card-navy p-5 border border-default/80">
          <h3 class="text-sm font-semibold text-primary mb-4">Actions par type</h3>
          <div class="flex flex-col md:flex-row items-center gap-6">
            <div class="w-36 h-36 rounded-full shrink-0 border border-default shadow-inner transition-transform hover:scale-105 duration-300" [style.background]="pieGradient" title="Répartition par type d'action"></div>
            <ul class="space-y-2 text-sm text-primary">
              @for (x of actionsByType; track x.label) {
                <li class="flex items-center gap-2">
                  <span class="w-3 h-3 rounded-sm shrink-0" [style.background]="x.color"></span>
                  {{ x.label }} — {{ x.value }}%
                </li>
              }
            </ul>
          </div>
        </div>
        <div class="card-navy p-5 border border-default/80">
          <div class="space-y-2">
            <p class="text-xs text-muted">Activité par jour (volume d'événements)</p>
            <div class="flex items-end gap-2 h-36">
              @for (d of activityByDay; track d.day) {
                <div class="flex-1 flex flex-col items-center gap-1">
                  <div class="w-full rounded-t bg-gradient-to-t from-blue-600/80 to-blue-400/40 min-h-[4px] transition-all duration-300 hover:from-blue-500/90" [style.height.%]="(d.v / maxActivity) * 100" [title]="d.v + ' événements'"></div>
                  <span class="text-[10px] text-muted">{{ d.day }}</span>
                </div>
              }
            </div>
          </div>
        </div>
      </div>

      <div class="card-navy p-5 border border-default/80">
        <h3 class="text-sm font-semibold text-primary mb-3">Répartition par rôle</h3>
        <div class="space-y-2">
          @for (r of actionsByRole; track r.role) {
            <div class="flex items-center gap-3">
              <span class="w-24 text-xs text-muted">{{ r.role }}</span>
              <div class="flex-1 h-2 rounded-full bg-card overflow-hidden">
                <div class="h-full bg-gradient-to-r from-soft-blue to-blue-400 transition-all duration-500" [style.width.%]="r.pct"></div>
              </div>
              <span class="w-10 text-xs text-muted text-right">{{ r.pct }}%</span>
            </div>
          }
        </div>
      </div>

      <div class="card-navy p-5 border border-default/80">
        <h3 class="text-sm font-semibold text-primary mb-3">Top utilisateurs actifs</h3>
        <div class="space-y-2">
          @for (u of topUsers; track u.name; let i = $index) {
            <div class="flex items-center justify-between gap-3 text-sm">
              <span class="text-muted w-6">{{ i + 1 }}.</span>
              <span class="flex-1 text-primary truncate">{{ u.name }}</span>
              <span class="text-muted tabular-nums">{{ u.actions }}</span>
            </div>
          }
        </div>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReportingDashboardComponent {
  readonly kpis = REPORTING_KPIS;
  readonly actionsByType = ACTIONS_BY_TYPE;
  readonly activityByDay = ACTIVITY_BY_DAY;
  readonly actionsByRole = ACTIONS_BY_ROLE;
  readonly topUsers = TOP_ACTIVE_USERS;

  readonly fileTextIcon = FileText;
  readonly downloadIcon = Download;
  readonly sheetIcon = FileSpreadsheet;

  readonly maxActivity = Math.max(...ACTIVITY_BY_DAY.map((d) => d.v), 1);

  get pieGradient(): string {
    const total = this.actionsByType.reduce((s, x) => s + x.value, 0);
    let acc = 0;
    const segments = this.actionsByType.map((x) => {
      const start = (acc / total) * 360;
      acc += x.value;
      const end = (acc / total) * 360;
      return `${x.color} ${start}deg ${end}deg`;
    });
    return `conic-gradient(${segments.join(', ')})`;
  }

  print(): void {
    window.print();
  }

  exportPdfHtml(): void {
    const k = this.kpis;
    const html = `<!DOCTYPE html><html><head><meta charset="utf-8"/><title>Rapport audit — PDF</title>
<style>body{font-family:system-ui;padding:24px;background:#0f172a;color:#e2e8f0;} h1{color:#93c5fd;} table{border-collapse:collapse;width:100%;margin-top:16px;} td,th{border:1px solid #334155;padding:8px;text-align:left;font-size:12px;}</style></head><body>
<h1>Rapport analytique — Audit Parrainage</h1>
<p>Période : mars 2026 (démo)</p>
<table>
<tr><th>Indicateur</th><th>Valeur</th></tr>
<tr><td>Total actions</td><td>${k.totalActions}</td></tr>
<tr><td>Actions / jour (moy.)</td><td>${k.actionsPerDay}</td></tr>
<tr><td>% actions critiques</td><td>${k.criticalPercent}%</td></tr>
<tr><td>Utilisateurs actifs</td><td>${k.activeUsers}</td></tr>
<tr><td>Anomalies détectées</td><td>${k.anomaliesCount}</td></tr>
<tr><td>Top utilisateur</td><td>${k.topUser} (${k.topUserActions} actions)</td></tr>
</table>
<p style="margin-top:24px;font-size:11px;color:#64748b;">Ouvrez via le navigateur puis Fichier → Imprimer → Enregistrer au format PDF.</p>
</body></html>`;
    const blob = new Blob([html], { type: 'text/html;charset=utf-8' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'rapport-audit-parrainage.html';
    a.click();
    URL.revokeObjectURL(a.href);
  }

  exportExcel(): void {
    const k = this.kpis;
    const tsv = toCsv(
      ['Indicateur', 'Valeur'],
      [
        ['Total actions', k.totalActions],
        ['Actions / jour (moy.)', k.actionsPerDay],
        ['% critiques', k.criticalPercent],
        ['Utilisateurs actifs', k.activeUsers],
        ['Anomalies', k.anomaliesCount],
        ['Top utilisateur', k.topUser],
      ],
    );
    download('synthese-audit.xls', tsv, 'application/vnd.ms-excel');
  }

  exportMonthlyCsv(): void {
    const csv = toCsv(
      ['Semaine', 'Jour', 'Volume'],
      this.activityByDay.map((d) => ['S12-2026', d.day, d.v]),
    );
    download('activite-mensuelle-audit.csv', csv, 'text/csv;charset=utf-8');
  }
}
