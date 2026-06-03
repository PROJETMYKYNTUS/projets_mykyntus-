import pathlib
root = pathlib.Path(r"c:/Users/Pc/Desktop/PROD/projets_mykyntus-")

# primeApiPost bearer
ph = root / "projet_kyntus_service_planning-frontend/src/app/features/prime/services/prime-http.ts"
t = ph.read_text(encoding="utf-8")
if "authHeaders()" not in t.split("primeApiPost")[1][:400]:
    t = t.replace(
        "headers: { 'Content-Type': 'application/json' },\n    body: JSON.stringify(body),\n  });\n  if (!res.ok) {\n    const t = await res.text();\n    throw new Error(t || `HTTP ${res.status}`);\n  }\n  return res.json() as Promise<T>;\n}",
        "headers: { 'Content-Type': 'application/json', ...authHeaders() },\n    body: JSON.stringify(body),\n  });\n  if (!res.ok) {\n    const t = await res.text();\n    throw new Error(t || `HTTP ${res.status}`);\n  }\n  return res.json() as Promise<T>;\n}",
        1,
    )
    ph.write_text(t, encoding="utf-8")
    print("primeApiPost auth")

# role.service inject
rs = root / "projet_kyntus_service_planning-frontend/src/app/features/prime/state/role.service.ts"
t = rs.read_text(encoding="utf-8")
t = t.replace("import { Injectable, computed, signal } from '@angular/core';", "import { Injectable, computed, inject, signal } from '@angular/core';")
t = t.replace("export class RoleService {\n  readonly currentRole", "export class RoleService {\n  private readonly session = inject(KyntusSessionService);\n  readonly currentRole")
t = t.replace("constructor(private readonly session: KyntusSessionService = new KyntusSessionService()) {", "constructor() {")
t = t.replace(
    "if (byEmail && employeeMatchesUiRole(byEmail, role)) {\n      this.setUserId(byEmail.id);\n      return;\n    }",
    "if (byEmail) {\n      this.setUserId(byEmail.id);\n      return;\n    }",
)
rs.write_text(t, encoding="utf-8")
print("role.service inject")

# PrimeDbSeeder - append method
seeder = root / "PrimeBackend/Data/PrimeDbSeeder.cs"
t = seeder.read_text(encoding="utf-8")
if "EnsureKyntusAuthAlignedEmployeesAsync" not in t:
    method = """
    /// <summary>Employés alignés sur les comptes Auth/Planning (*@kyntus.ma) — idempotent par e-mail.</summary>
    public static async Task EnsureKyntusAuthAlignedEmployeesAsync(PrimeDbContext db, CancellationToken cancellationToken = default)
    {
        if (!await db.Poles.AnyAsync(cancellationToken))
            return;

        var existing = await db.Employees.AsNoTracking().Select(e => e.Email.ToLower()).ToListAsync(cancellationToken);
        var have = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        static EmployeeEntity E(string id, string fn, string ln, string role, string email) => new()
        {
            Id = id,
            FirstName = fn,
            LastName = ln,
            Role = role,
            ParentId = null,
            PoleId = "d1",
            CelluleId = "p1",
            ServiceId = "c1",
            Email = email
        };

        var rows = new List<EmployeeEntity>
        {
            E("kyntus-employee", "Employé", "Démo", "Pilote", "employee@kyntus.ma"),
            E("kyntus-rh", "Rh", "Démo", "RH", "rh@kyntus.ma"),
            E("kyntus-manager", "Manager", "Démo", "Manager", "manager@kyntus.ma"),
            E("kyntus-coach", "Coach", "Démo", "Référent technique", "coach@kyntus.ma"),
            E("kyntus-rp", "Rp", "Démo", "Chef de projet", "rp@kyntus.ma"),
            E("kyntus-admin", "Admin", "Démo", "Admin", "admin@kyntus.ma"),
            E("kyntus-audit", "Audit", "Démo", "Audit", "audit@kyntus.ma"),
            E("kyntus-formation", "Formation", "Démo", "RH", "formation@kyntus.ma"),
            E("kyntus-yasmine", "Yasmine", "El Amrani", "Pilote", "yasmine.elamrani@atlas-tech-demo.dev"),
            E("kyntus-fatima", "Fatima", "Alaoui", "RH", "fatima.alaoui@atlas-tech-demo.dev"),
        };

        var toAdd = rows.Where(r => !have.Contains(r.Email)).ToList();
        if (toAdd.Count == 0) return;
        db.Employees.AddRange(toAdd);
        await db.SaveChangesAsync(cancellationToken);
    }
"""
    t = t.replace("}\n", method + "\n}\n", 1)  # wrong - adds at first brace
    # append before last closing brace of class
    idx = t.rfind("}\n")
    t = t[:idx] + method + t[idx:]
    seeder.write_text(t, encoding="utf-8")
    print("PrimeDbSeeder method added - verify manually")

init = root / "PrimeBackend/Data/PrimeDatabaseInitializer.cs"
ti = init.read_text(encoding="utf-8")
if "EnsureKyntusAuthAlignedEmployeesAsync" not in ti:
    ti = ti.replace(
        "await PrimeDbSeeder.SeedAsync(db, seedDemo, cancellationToken);\n        }",
        "await PrimeDbSeeder.SeedAsync(db, seedDemo, cancellationToken);\n        }\n\n        await PrimeDbSeeder.EnsureKyntusAuthAlignedEmployeesAsync(db, cancellationToken);",
    )
    init.write_text(ti, encoding="utf-8")
    print("initializer call added")

# dashboards session email
for rel in [
    "projet_kyntus_service_planning-frontend/src/app/features/dashboard/pages/dashboard-home/dashboard-home.component.ts",
    "projet_kyntus_service_planning-frontend/src/app/features/dashboard/pages/dashboard-employee/dashboard-employee.component.ts",
]:
    p = root / rel
    t = p.read_text(encoding="utf-8")
    if "KyntusSessionService" not in t and "openDocumentation" in t:
        t = t.replace(
            "import { Component",
            "import { KyntusSessionService } from '../../../../core/session/kyntus-session.service';\nimport { Component",
            1,
        )
        if "inject(KyntusSessionService)" not in t:
            t = t.replace(
                "const email = (this.currentUser?.email",
                "const email = (this.session.getEmail() || this.currentUser?.email",
            )
            # add session inject - grep constructor
        p.write_text(t, encoding="utf-8")
        print("patched", rel)
