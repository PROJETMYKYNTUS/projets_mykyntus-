import pathlib, re
root = pathlib.Path(r"c:/Users/Pc/Desktop/PROD/projets_mykyntus-/projet_kyntus_service_planning-frontend/src/app")

# documentation-shell: inject KyntusSessionService
shell = root / "features/documentation/documentation-feature/components/documentation-shell/documentation-shell.component.ts"
t = shell.read_text(encoding="utf-8")
if "KyntusSessionService" not in t:
    t = t.replace(
        "import { DocumentationNavigationService }",
        "import { KyntusSessionService } from '../../../../../core/session/kyntus-session.service';\nimport { KYNTUS_DEFAULT_TENANT } from '../../../../../core/session/kyntus-session.constants';\nimport { DocumentationNavigationService }",
    )
    t = t.replace(
        "private readonly router: Router,\n  ) {}",
        "private readonly router: Router,\n    private readonly session: KyntusSessionService,\n  ) {}",
    )
    t = t.replace(
        "if (!this.identity.getTenantId()) {\n      this.identity.setTenantId('atlas-tech-demo');\n    }",
        "if (!this.identity.getTenantId()) {\n      this.identity.setTenantId(KYNTUS_DEFAULT_TENANT);\n    }",
    )
    t = t.replace(
        "private readPlanningLoginEmail(): string | null {\n    try {\n      const raw = localStorage.getItem('user');\n      if (!raw) {\n        return null;\n      }\n      const parsed = JSON.parse(raw) as { email?: string; username?: string };\n      const email = (parsed.email ?? parsed.username ?? '').trim();\n      return email.includes('@') ? email : null;\n    } catch {\n      return null;\n    }\n  }",
        "private readPlanningLoginEmail(): string | null {\n    const email = this.session.getEmail();\n    return email.includes('@') ? email : null;\n  }",
    )
    shell.write_text(t, encoding="utf-8")
    print("documentation-shell ok")

# prime-http bearer
ph = root / "features/prime/services/prime-http.ts"
t = ph.read_text(encoding="utf-8")
if "Authorization" not in t:
    t = t.replace(
        "export const PRIME_API_BASE =",
        "function authHeaders(): Record<string, string> {\n  const token = localStorage.getItem('token') || localStorage.getItem('accessToken');\n  const h: Record<string, string> = {};\n  if (token) h['Authorization'] = `Bearer ${token}`;\n  return h;\n}\n\nexport const PRIME_API_BASE =",
    )
    t = t.replace(
        "const res = await fetch(full, { credentials: 'include' });",
        "const res = await fetch(full, { credentials: 'include', headers: authHeaders() });",
    )
    t = t.replace(
        "const res = await fetch(`${PRIME_API_BASE}${path}`, {\n    method: 'PUT',\n    credentials: 'include',\n    headers: { 'Content-Type': 'application/json' },",
        "const res = await fetch(`${PRIME_API_BASE}${path}`, {\n    method: 'PUT',\n    credentials: 'include',\n    headers: { 'Content-Type': 'application/json', ...authHeaders() },",
    )
    ph.write_text(t, encoding="utf-8")
    print("prime-http ok")

# role.service JWT first + email match
rs = root / "features/prime/state/role.service.ts"
t = rs.read_text(encoding="utf-8")
if "KyntusSessionService" not in t:
    t = t.replace("import { primeApiGet }", "import { KyntusSessionService } from '../../../core/session/kyntus-session.service';\nimport { findEmployeeByLoginEmail } from '../lib/prime-demo-users';\nimport { primeApiGet }")
    t = t.replace(
        "export class RoleService {",
        "export class RoleService {\n  private readonly session = new KyntusSessionService();",
    )
    # inject() not available in class field with new - use inject in constructor instead
    t = t.replace(
        "export class RoleService {\n  private readonly session = new KyntusSessionService();",
        "export class RoleService {",
    )
    t = t.replace(
        "constructor() {",
        "constructor(private readonly session: KyntusSessionService = new KyntusSessionService()) {",
    )
    t = t.replace(
        """  private static readStoredRole(): Role {
    try {
      const saved = sessionStorage.getItem(ROLE_STORAGE_KEY) as Role | null;
      if (saved && PRIME_AUTHORIZED_ROLES.includes(saved)) return saved;
    } catch {
      /* ignore */
    }
    const fromJwt = mapJwtToPrimeRole(RoleService.readJwtRole());
    if (fromJwt && PRIME_AUTHORIZED_ROLES.includes(fromJwt)) return fromJwt;
    return 'Superviseur';
  }""",
        """  private static readStoredRole(): Role {
    const fromJwt = mapJwtToPrimeRole(RoleService.readJwtRole());
    if (fromJwt && PRIME_AUTHORIZED_ROLES.includes(fromJwt)) return fromJwt;
    try {
      const saved = sessionStorage.getItem(ROLE_STORAGE_KEY) as Role | null;
      if (saved && PRIME_AUTHORIZED_ROLES.includes(saved)) return saved;
    } catch {
      /* ignore */
    }
    return 'Superviseur';
  }""",
    )
    t = t.replace(
        "  private ensureUserMatchesRole(): void {\n    const role = this.currentRole();\n    const list = this.employees();\n    if (list.length === 0) return;\n    const stored = this.selectedUserId();\n    const resolved = resolveEmployeeForRole(list, role, stored);\n    if (!stored || stored !== resolved.id) this.setUserId(resolved.id);\n  }",
        """  private ensureUserMatchesRole(): void {
    const role = this.currentRole();
    const list = this.employees();
    if (list.length === 0) return;
    const email = this.session.getEmail();
    const byEmail = email ? findEmployeeByLoginEmail(list, email) : undefined;
    if (byEmail && employeeMatchesUiRole(byEmail, role)) {
      this.setUserId(byEmail.id);
      return;
    }
    const stored = this.selectedUserId();
    const resolved = resolveEmployeeForRole(list, role, stored);
    if (!stored || stored !== resolved.id) this.setUserId(resolved.id);
  }""",
    )
    if "employeeMatchesUiRole" not in t.split("ensureUserMatchesRole")[0][-500:]:
        t = t.replace(
            "import {\n  employeesForUiRole,\n  pickDefaultEmployeeForRole,\n  resolveEmployeeForRole,\n} from '../lib/prime-demo-users';",
            "import {\n  employeesForUiRole,\n  employeeMatchesUiRole,\n  pickDefaultEmployeeForRole,\n  resolveEmployeeForRole,\n} from '../lib/prime-demo-users';",
        )
    rs.write_text(t, encoding="utf-8")
    print("role.service ok")

# prime-demo-users findEmployeeByLoginEmail
pd = root / "features/prime/lib/prime-demo-users.ts"
t = pd.read_text(encoding="utf-8")
if "findEmployeeByLoginEmail" not in t:
    t = t.replace(
        "export function employeesForUiRole",
        "export function findEmployeeByLoginEmail(list: Employee[], email: string): Employee | undefined {\n  const needle = email.trim().toLowerCase();\n  if (!needle) return undefined;\n  return list.find((e) => (e.email ?? '').trim().toLowerCase() === needle);\n}\n\nexport function employeesForUiRole",
    )
    pd.write_text(t, encoding="utf-8")
    print("prime-demo-users ok")

# auth.service thin wrapper
auth = root / "core/services/auth.service.ts"
t = auth.read_text(encoding="utf-8")
if "KyntusSessionService" not in t:
    t = """import { Injectable, inject } from '@angular/core';
import { KyntusSessionService } from '../session/kyntus-session.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly session = inject(KyntusSessionService);

  getAuthUserId(): number { return this.session.getAuthUserId(); }
  getRole(): string { return this.session.getRole(); }
  getEmail(): string { return this.session.getEmail(); }
}
"""
    auth.write_text(t, encoding="utf-8")
    print("auth.service ok")

print("frontend patches done")
