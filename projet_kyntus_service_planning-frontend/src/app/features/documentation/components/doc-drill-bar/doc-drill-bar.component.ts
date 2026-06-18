import { CommonModule } from '@angular/common';
import { Component, Input, OnDestroy, OnInit } from '@angular/core';
import { Subscription } from 'rxjs';

import { DocumentationIdentityService } from '../../../../core/services/documentation-identity.service';
import type { DocumentationRole } from '../../interfaces/documentation-role';
import { drillSelectOptions } from '../../lib/documentation-org-hierarchy';
import { DocumentationHierarchyDrillService } from '../../services/documentation-hierarchy-drill.service';

@Component({
  selector: 'app-doc-drill-bar',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './doc-drill-bar.component.html',
})
export class DocDrillBarComponent implements OnInit, OnDestroy {
  @Input({ required: true }) role!: DocumentationRole;

  drill = this.hierarchy.drill;
  managers: { value: string; label: string }[] = [];
  coaches: { value: string; label: string }[] = [];
  pilots: { value: string; label: string }[] = [];
  private sub = new Subscription();

  constructor(
    private readonly hierarchy: DocumentationHierarchyDrillService,
    private readonly identity: DocumentationIdentityService,
  ) {}

  ngOnInit(): void {
    this.sub.add(
      this.hierarchy.drill$.subscribe((d) => {
        this.drill = d;
        this.refreshOptions();
      }),
    );
    this.sub.add(this.identity.profile$.subscribe(() => this.refreshOptions()));
    this.sub.add(this.identity.directoryUsers$.subscribe(() => this.refreshOptions()));
    this.refreshOptions();
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }

  onManagerChange(value: string): void {
    const nextManagerId = value || undefined;
    this.hierarchy.setManagerId(nextManagerId);
    this.identity.setOrgScope(nextManagerId ?? null, null);
    this.identity.bumpContextRevision();
  }

  onCoachChange(value: string): void {
    const nextCoachId = value || undefined;
    this.hierarchy.setCoachId(nextCoachId);
    if (this.role === 'Manager') {
      this.identity.setOrgScope(null, nextCoachId ?? null);
      this.identity.bumpContextRevision();
      return;
    }
    if (this.role === 'RP') {
      this.identity.setOrgScope(this.drill.managerId ?? null, nextCoachId ?? null);
      this.identity.bumpContextRevision();
    }
  }

  onPilotChange(value: string): void {
    this.hierarchy.setPilotId(value || undefined);
  }

  private refreshOptions(): void {
    const viewerId = this.identity.profile$.value?.id ?? '';
    const users = this.identity.directoryUsers$.value;
    const { managers, coaches, pilots } = drillSelectOptions(this.role, users, viewerId, this.drill);
    this.managers = managers;
    this.coaches = coaches;
    this.pilots = pilots;
  }
}
