import pathlib
root = pathlib.Path(r"c:/Users/Pc/Desktop/PROD/projets_mykyntus-/projet_kyntus_service_planning-frontend/src/app/features/dashboard/pages")
for name, cls in [("dashboard-home/dashboard-home.component.ts", "DashboardHomeComponent"), ("dashboard-employee/dashboard-employee.component.ts", "DashboardEmployeeComponent")]:
    p = root / name
    t = p.read_text(encoding="utf-8")
    if "private readonly session" in t:
        continue
    if "import { inject }" not in t:
        t = t.replace("import { Component", "import { inject } from '@angular/core';\nimport { Component", 1)
    marker = "export class " + cls + " implements OnInit {"
    t = t.replace(marker, marker + "\n  private readonly session = inject(KyntusSessionService);", 1)
    if "this.session.getEmail()" not in t and "openDocumentation" in t:
        t = t.replace("(this.currentUser?.email", "(this.session.getEmail() || this.currentUser?.email")
    p.write_text(t, encoding="utf-8")
    print("fixed", name)
