// src/app/shared/directives/has-permission.directive.ts

import { Directive, Input, OnInit, TemplateRef, ViewContainerRef } from '@angular/core';
import { PermissionService } from '../../../core/services/permissions.service';

@Directive({
  selector: '[hasPermission]',
  standalone: true // ✅ ajouté
})
export class HasPermissionDirective implements OnInit {

  @Input() hasPermission!: [string, string]; // [feature, action]

  constructor(
    private tpl: TemplateRef<any>,
    private vcr: ViewContainerRef,
    private permissionService: PermissionService
  ) {}

  ngOnInit(): void {
    const [feature, action] = this.hasPermission;

    if (this.permissionService.hasPermission(feature, action)) {
      this.vcr.createEmbeddedView(this.tpl);
    } else {
      this.vcr.clear();
    }
  }
}