import { Injectable, inject } from '@angular/core';

import { Router } from '@angular/router';

import { MICROSERVICES, Microservice, MenuItem } from './microservices.config';

import { buildPrimeMenuItemsForRole } from './prime-menu.config';

import { buildParrainageMenuItemsForRole } from './parrainage-menu.config';

import {

  buildDocumentationMenuItemsForRole,

  mapJwtRoleToDocumentationRole,

} from './documentation-menu.config';

import { RoleService } from '../../features/prime/state/role.service';

import { PrimeUiPermissionsService } from '../../features/prime/services/prime-ui-permissions.service';

import { isPrimePathAllowedForRole } from '../../features/prime/lib/prime-nav-access';

import { buildPrimeDepartmentManagerNav } from '../../features/prime/lib/prime-manager-nav';

import { ParrainageRoleService } from '../../features/parrainage/state/parrainage-role.service';

import { DocumentationNavigationService } from '../../features/documentation/services/documentation-navigation.service';
import { roleNamesMatch } from '../org/org-role-assignment';

import { DepartmentContextService } from '../../features/prime/services/allowance-api.service';
import { AllowanceInboxBadgeService } from '../../features/prime/services/allowance-inbox-badge.service';

import type { Role } from '../../features/prime/models';



@Injectable({ providedIn: 'root' })

export class NavigationMenuService {

  private readonly primeRole = inject(RoleService);

  private readonly primePermissions = inject(PrimeUiPermissionsService);

  private readonly parrainageRole = inject(ParrainageRoleService);

  private readonly docNav = inject(DocumentationNavigationService);

  private readonly deptContext = inject(DepartmentContextService);

  private readonly inboxBadge = inject(AllowanceInboxBadgeService);

  private readonly router = inject(Router);



  buildVisibleGroups(jwtRole: string): Microservice[] {

    return MICROSERVICES.map((g) => this.resolveGroup(g, jwtRole))

      .filter((g) => this.jwtRoleAllowed(g.roles, jwtRole) && g.children.length > 0);

  }



  private resolveGroup(group: Microservice, jwtRole: string): Microservice {

    if (group.id === 'prime' && group.dynamicChildren) {

      return { ...group, children: this.filterPrimeChildren() };

    }

    if (group.id === 'parrainage' && group.dynamicChildren) {

      return { ...group, children: this.filterParrainageChildren() };

    }

    if (group.id === 'documentation' && group.dynamicChildren) {

      return { ...group, children: this.filterDocumentationChildren(jwtRole) };

    }

    return {

      ...group,

      children: group.children.filter((c) => this.jwtRoleAllowed(c.roles, jwtRole)),

    };

  }



  private managerNav() {

    return buildPrimeDepartmentManagerNav(this.deptContext);

  }



  private filterPrimeChildren(): MenuItem[] {

    const role = this.primeRole.currentRole();

    const kind = this.deptContext.departmentKind();

    const managerNav = this.managerNav();

    const items = buildPrimeMenuItemsForRole(role, kind, managerNav).filter((item) =>

      this.isPrimeItemAllowed(item, role),

    );

    const badgeCount = this.inboxBadge.count();

    return items.map((item) => {

      if (item.primePath === '/allowances/inbox' && role === 'RH' && badgeCount > 0) {

        return { ...item, menuBadge: badgeCount };

      }

      return item;

    });

  }



  private isPrimeItemAllowed(item: MenuItem, role: Role): boolean {

    if (item.isSectionHeader) return true;

    if (item.primeAdminSection || item.primeRpSection || item.primeAuditSection) {

      return true;

    }

    const path = item.primePath;

    if (!path) return false;

    const managerNav = this.managerNav();

    if (!isPrimePathAllowedForRole(path, role, this.deptContext.departmentKind(), managerNav)) {

      return false;

    }

    return this.primePermissions.canViewPath(role, path, managerNav);

  }



  private filterParrainageChildren(): MenuItem[] {

    const role = this.parrainageRole.user().role;

    return buildParrainageMenuItemsForRole(role);

  }



  private filterDocumentationChildren(jwtRole: string): MenuItem[] {

    const fromJwt = mapJwtRoleToDocumentationRole(jwtRole);

    const path = this.router.url.split('?')[0];

    const docRole =

      path.startsWith('/documentation') && this.docNav.role !== 'Pilote'

        ? this.docNav.role

        : fromJwt;

    return buildDocumentationMenuItemsForRole(docRole);

  }



  private jwtRoleAllowed(roles: string[] | undefined, jwtRole: string): boolean {

    if (!roles || roles.length === 0) return true;

    return roles.some((x) => roleNamesMatch(x, jwtRole));

  }

}


