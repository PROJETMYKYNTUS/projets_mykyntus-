import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Archive, BookOpen, Plus, Trash2 } from 'lucide';
import { FormationTrainingService } from '../../../core/services/formation-training.service';
import type {
  LearningQuizResultExportRowDto,
  LearningQuizStatsDto,
  TrainingCatalogItemDto,
  TrainingModuleDto,
} from '../../../core/models/formation-training.models';
import { KyntusPageHeaderComponent } from '../../../shared/components/ui/kyntus-page-header.component';
import { LucideIconComponent } from '../../../shared/lucide-icon.component';

@Component({
  selector: 'app-formation-catalog-admin',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, KyntusPageHeaderComponent, LucideIconComponent],
  templateUrl: './formation-catalog-admin.component.html',
  styleUrls: ['./formation-catalog-admin.component.css'],
})
export class FormationCatalogAdminComponent implements OnInit {
  readonly icons = { add: Plus, book: BookOpen, trash: Trash2, archive: Archive };
  private readonly api = inject(FormationTrainingService);

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly message = signal<string | null>(null);
  readonly items = signal<TrainingCatalogItemDto[]>([]);
  readonly selected = signal<TrainingCatalogItemDto | null>(null);
  readonly stats = signal<LearningQuizStatsDto | null>(null);
  readonly exportRows = signal<LearningQuizResultExportRowDto[]>([]);
  readonly tab = signal<'list' | 'edit' | 'stats'>('list');
  includeArchived = false;

  draft = {
    title: '',
    description: '',
    category: '',
    defaultGateMode: 'Content',
    audienceMatchMode: 'MatchAny',
  };
  audience = {
    rolesText: '',
    structuresText: '',
    userIdsText: '',
  };
  moduleDraft = { title: '', description: '', sortOrder: 0 };
  lessonDraft = { moduleId: '', title: '', description: '', sortOrder: 0, isRequired: true };
  resourceDraft = {
    lessonId: '',
    title: '',
    type: 'Pdf',
    url: '',
    textContent: '',
    sortOrder: 0,
  };

  ngOnInit(): void {
    void this.reload();
  }

  async reload(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.items.set(await this.api.listCatalog(this.includeArchived));
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Chargement impossible');
    } finally {
      this.loading.set(false);
    }
  }

  openCreate(): void {
    this.selected.set(null);
    this.draft = {
      title: '',
      description: '',
      category: '',
      defaultGateMode: 'Content',
      audienceMatchMode: 'MatchAny',
    };
    this.audience = { rolesText: '', structuresText: '', userIdsText: '' };
    this.tab.set('edit');
  }

  async openEdit(id: string): Promise<void> {
    this.busy.set(true);
    try {
      const item = await this.api.getCatalogItem(id);
      this.selected.set(item);
      this.draft = {
        title: item.title,
        description: item.description,
        category: item.category,
        defaultGateMode: String(item.defaultGateMode ?? 'Content'),
        audienceMatchMode: String(item.audienceMatchMode ?? 'MatchAny'),
      };
      this.audience = {
        rolesText: (item.audience?.roles ?? []).join(', '),
        structuresText: (item.audience?.structureKeys ?? []).join(', '),
        userIdsText: (item.audience?.userIds ?? []).join(', '),
      };
      this.tab.set('edit');
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Chargement détail impossible');
    } finally {
      this.busy.set(false);
    }
  }

  async saveItem(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    this.message.set(null);
    try {
      const body = {
        title: this.draft.title.trim(),
        description: this.draft.description.trim(),
        category: this.draft.category.trim(),
        defaultGateMode: this.toGateMode(this.draft.defaultGateMode),
        audienceMatchMode: this.toMatchMode(this.draft.audienceMatchMode),
      };
      let saved: TrainingCatalogItemDto;
      if (this.selected()?.id) {
        saved = await this.api.updateCatalogItem(this.selected()!.id, body);
      } else {
        saved = await this.api.createCatalogItem(body);
      }
      await this.api.upsertCatalogAudience(saved.id, {
        matchMode: this.toMatchMode(this.draft.audienceMatchMode),
        roles: this.splitCsv(this.audience.rolesText),
        structureKeys: this.splitCsv(this.audience.structuresText),
        userIds: this.splitCsv(this.audience.userIdsText),
      });
      this.message.set('Formation catalogue enregistrée.');
      await this.openEdit(saved.id);
      await this.reload();
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Enregistrement impossible');
    } finally {
      this.busy.set(false);
    }
  }

  async publish(): Promise<void> {
    const id = this.selected()?.id;
    if (!id) return;
    this.busy.set(true);
    try {
      await this.api.publishCatalogItem(id);
      this.message.set('Formation publiée.');
      await this.openEdit(id);
      await this.reload();
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Publication impossible');
    } finally {
      this.busy.set(false);
    }
  }

  async archive(): Promise<void> {
    const id = this.selected()?.id;
    if (!id) return;
    this.busy.set(true);
    try {
      await this.api.archiveCatalogItem(id);
      this.message.set('Formation archivée.');
      this.tab.set('list');
      await this.reload();
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Archivage impossible');
    } finally {
      this.busy.set(false);
    }
  }

  async addModule(): Promise<void> {
    const id = this.selected()?.id;
    if (!id || !this.moduleDraft.title.trim()) return;
    this.busy.set(true);
    try {
      await this.api.createCatalogModule(id, { ...this.moduleDraft, title: this.moduleDraft.title.trim() });
      this.moduleDraft = { title: '', description: '', sortOrder: (this.selected()?.modules?.length ?? 0) + 1 };
      await this.openEdit(id);
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Module impossible');
    } finally {
      this.busy.set(false);
    }
  }

  async addLesson(): Promise<void> {
    const id = this.selected()?.id;
    if (!id || !this.lessonDraft.moduleId || !this.lessonDraft.title.trim()) return;
    this.busy.set(true);
    try {
      await this.api.createCatalogLesson(this.lessonDraft.moduleId, {
        title: this.lessonDraft.title.trim(),
        description: this.lessonDraft.description,
        sortOrder: this.lessonDraft.sortOrder,
        isRequired: this.lessonDraft.isRequired,
      });
      this.lessonDraft = { ...this.lessonDraft, title: '', description: '' };
      await this.openEdit(id);
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Leçon impossible');
    } finally {
      this.busy.set(false);
    }
  }

  async addResource(): Promise<void> {
    const id = this.selected()?.id;
    if (!id || !this.resourceDraft.lessonId || !this.resourceDraft.title.trim()) return;
    this.busy.set(true);
    try {
      await this.api.createCatalogResource(this.resourceDraft.lessonId, {
        type: this.toResourceType(this.resourceDraft.type),
        title: this.resourceDraft.title.trim(),
        url: this.resourceDraft.url || null,
        textContent: this.resourceDraft.textContent || null,
        sortOrder: this.resourceDraft.sortOrder,
      });
      this.resourceDraft = { ...this.resourceDraft, title: '', url: '', textContent: '' };
      await this.openEdit(id);
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Ressource impossible');
    } finally {
      this.busy.set(false);
    }
  }

  async uploadResource(ev: Event): Promise<void> {
    const id = this.selected()?.id;
    const input = ev.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!id || !file || !this.resourceDraft.lessonId) return;
    this.busy.set(true);
    try {
      await this.api.uploadCatalogResource(
        this.resourceDraft.lessonId,
        file,
        this.resourceDraft.title || file.name,
        this.resourceDraft.type,
      );
      this.message.set('Fichier uploadé.');
      await this.openEdit(id);
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Upload impossible');
    } finally {
      this.busy.set(false);
      input.value = '';
    }
  }

  async deleteModule(module: TrainingModuleDto): Promise<void> {
    const id = this.selected()?.id;
    if (!id || !confirm(`Supprimer le module « ${module.title} » ?`)) return;
    await this.api.deleteCatalogModule(id, module.id);
    await this.openEdit(id);
  }

  async loadStats(): Promise<void> {
    this.tab.set('stats');
    this.busy.set(true);
    try {
      this.stats.set(await this.api.getLearningStats());
      this.exportRows.set(await this.api.exportLearningResults());
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Stats impossibles');
    } finally {
      this.busy.set(false);
    }
  }

  exportCsv(): void {
    const rows = this.exportRows();
    const header = ['Collaborateur', 'Email', 'Rôle', 'Structure', 'Session', 'Score', 'Réussi', 'Tentative', 'Date'];
    const lines = [
      header.join(';'),
      ...rows.map((r) =>
        [
          r.employeeName,
          r.email,
          r.role,
          r.structureKey,
          r.sessionTitle,
          r.score ?? '',
          r.passed == null ? '' : r.passed ? 'Oui' : 'Non',
          r.attemptNumber,
          r.submittedAt,
        ]
          .map((v) => `"${String(v).replace(/"/g, '""')}"`)
          .join(';'),
      ),
    ];
    const blob = new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `resultats_formation_${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  statusLabel(status: string | number): string {
    if (status === 0 || status === 'Draft') return 'Brouillon';
    if (status === 1 || status === 'Published') return 'Publiée';
    if (status === 2 || status === 'Archived') return 'Archivée';
    return String(status);
  }

  private splitCsv(value: string): string[] {
    return value
      .split(/[,;\n]/)
      .map((x) => x.trim())
      .filter(Boolean);
  }

  private toGateMode(v: string): number {
    if (v === 'Attendance' || v === '0') return 0;
    if (v === 'Both' || v === '2') return 2;
    return 1;
  }

  private toMatchMode(v: string): number {
    return v === 'MatchAll' || v === '1' ? 1 : 0;
  }

  private toResourceType(v: string): number {
    if (v === 'Video' || v === '1') return 1;
    if (v === 'Link' || v === '2') return 2;
    if (v === 'Text' || v === '3') return 3;
    return 0;
  }
}
