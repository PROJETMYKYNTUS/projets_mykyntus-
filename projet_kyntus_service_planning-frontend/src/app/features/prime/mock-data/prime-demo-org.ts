import type { Department, Employee } from '../models';

/** Kyntus Maroc — structure Pôle → Cellule → Service (site unique : Oujda). */
export const DEMO_DEPARTMENTS: Department[] = [
  {
    id: 'dept-delivery',
    name: 'Pôle Delivery & Engineering',
    poles: [
      {
        id: 'pole-apps',
        name: 'Applications Métier',
        departmentId: 'dept-delivery',
        cells: [
          {
            id: 'cell-crm',
            name: 'Cellule CRM & Billing',
            poleId: 'pole-apps',
            teams: [
              { id: 'svc-crm-core', name: 'Service CRM Core', celluleId: 'cell-crm' },
              { id: 'svc-billing', name: 'Service Facturation', celluleId: 'cell-crm' },
            ],
          },
          {
            id: 'cell-integration',
            name: 'Cellule Intégration SI',
            poleId: 'pole-apps',
            teams: [
              { id: 'svc-api', name: 'Service API & Middleware', celluleId: 'cell-integration' },
              { id: 'svc-edi', name: 'Service EDI B2B', celluleId: 'cell-integration' },
            ],
          },
        ],
      },
      {
        id: 'pole-cloud',
        name: 'Cloud & DevOps',
        departmentId: 'dept-delivery',
        cells: [
          {
            id: 'cell-devops',
            name: 'Cellule Infrastructure',
            poleId: 'pole-cloud',
            teams: [
              { id: 'svc-devops', name: 'Service DevOps Oujda', celluleId: 'cell-devops' },
              { id: 'svc-secops', name: 'Service SecOps', celluleId: 'cell-devops' },
            ],
          },
        ],
      },
    ],
  },
  {
    id: 'dept-support',
    name: 'Pôle Support & Qualité',
    poles: [
      {
        id: 'pole-support',
        name: 'Centre de contacts',
        departmentId: 'dept-support',
        cells: [
          {
            id: 'cell-cc',
            name: 'Cellule Relation Client',
            poleId: 'pole-support',
            teams: [
              { id: 'svc-cc-oujda-n1', name: 'Service N1 Oujda — relation client', celluleId: 'cell-cc' },
              { id: 'svc-cc-oujda-n2', name: 'Service N2 Oujda — réclamations', celluleId: 'cell-cc' },
            ],
          },
        ],
      },
    ],
  },
];

export const DEMO_EMPLOYEES: Employee[] = [
  {
    id: 'e-cdp-omar',
    firstName: 'Omar',
    lastName: 'Chraibi',
    role: 'Chef de projet',
    poleId: 'pole-apps',
    celluleId: 'cell-crm',
    serviceId: 'svc-crm-core',
    email: 'omar.chraibi@kyntus.ma',
  },
  {
    id: 'e-sup-nadia',
    firstName: 'Nadia',
    lastName: 'Benjelloun',
    role: 'Superviseur',
    parentId: 'e-cdp-omar',
    poleId: 'pole-apps',
    celluleId: 'cell-crm',
    serviceId: 'svc-crm-core',
    email: 'nadia.benjelloun@kyntus.ma',
  },
  {
    id: 'e-rt-kenza',
    firstName: 'Kenza',
    lastName: 'Alami',
    role: 'Référent technique',
    parentId: 'e-sup-nadia',
    poleId: 'pole-apps',
    celluleId: 'cell-crm',
    serviceId: 'svc-crm-core',
    email: 'kenza.alami@kyntus.ma',
  },
  {
    id: 'e-rt-youssef',
    firstName: 'Youssef',
    lastName: 'Idrissi',
    role: 'Référent technique',
    parentId: 'e-sup-nadia',
    poleId: 'pole-apps',
    celluleId: 'cell-crm',
    serviceId: 'svc-billing',
    email: 'youssef.idrissi@kyntus.ma',
  },
  {
    id: 'e-pil-salma',
    firstName: 'Salma',
    lastName: 'Bennani',
    role: 'Pilote',
    parentId: 'e-rt-kenza',
    poleId: 'pole-apps',
    celluleId: 'cell-crm',
    serviceId: 'svc-crm-core',
    email: 'salma.bennani@kyntus.ma',
  },
  {
    id: 'e-pil-mehdi',
    firstName: 'Mehdi',
    lastName: 'Tazi',
    role: 'Pilote',
    parentId: 'e-rt-kenza',
    poleId: 'pole-apps',
    celluleId: 'cell-crm',
    serviceId: 'svc-crm-core',
    email: 'mehdi.tazi@kyntus.ma',
  },
  {
    id: 'e-pil-hind',
    firstName: 'Hind',
    lastName: 'Alaoui',
    role: 'Pilote',
    parentId: 'e-rt-youssef',
    poleId: 'pole-apps',
    celluleId: 'cell-crm',
    serviceId: 'svc-billing',
    email: 'hind.alaoui@kyntus.ma',
  },
  {
    id: 'e-pil-karim',
    firstName: 'Karim',
    lastName: 'Berrada',
    role: 'Pilote',
    parentId: 'e-rt-youssef',
    poleId: 'pole-apps',
    celluleId: 'cell-crm',
    serviceId: 'svc-billing',
    email: 'karim.berrada@kyntus.ma',
  },
  {
    id: 'e-pil-fatima',
    firstName: 'Fatima Zahra',
    lastName: 'Ouazzani',
    role: 'Pilote',
    parentId: 'e-rt-kenza',
    poleId: 'pole-apps',
    celluleId: 'cell-integration',
    serviceId: 'svc-api',
    email: 'fatima.ouazzani@kyntus.ma',
  },
  {
    id: 'e-sup-rachid',
    firstName: 'Rachid',
    lastName: 'El Amrani',
    role: 'Superviseur',
    parentId: 'e-cdp-omar',
    poleId: 'pole-cloud',
    celluleId: 'cell-devops',
    serviceId: 'svc-devops',
    email: 'rachid.elamrani@kyntus.ma',
  },
  {
    id: 'e-rt-sanae',
    firstName: 'Sanae',
    lastName: 'Mouline',
    role: 'Référent technique',
    parentId: 'e-sup-rachid',
    poleId: 'pole-cloud',
    celluleId: 'cell-devops',
    serviceId: 'svc-devops',
    email: 'sanae.mouline@kyntus.ma',
  },
  {
    id: 'e-pil-amine',
    firstName: 'Amine',
    lastName: 'Fassi',
    role: 'Pilote',
    parentId: 'e-rt-sanae',
    poleId: 'pole-cloud',
    celluleId: 'cell-devops',
    serviceId: 'svc-devops',
    email: 'amine.fassi@kyntus.ma',
  },
  {
    id: 'e-mgr-laila',
    firstName: 'Laila',
    lastName: 'Sqalli',
    role: 'Manager',
    poleId: 'pole-apps',
    celluleId: 'cell-crm',
    serviceId: 'svc-crm-core',
    email: 'laila.sqalli@kyntus.ma',
  },
  {
    id: 'e-rh-ines',
    firstName: 'Inès',
    lastName: 'Bouazza',
    role: 'RH',
    poleId: 'pole-apps',
    celluleId: 'cell-crm',
    serviceId: 'svc-crm-core',
    email: 'ines.bouazza@kyntus.ma',
  },
  {
    id: 'e-cpta-hamza',
    firstName: 'Hamza',
    lastName: 'Kettani',
    role: 'Comptabilité',
    poleId: 'pole-apps',
    celluleId: 'cell-crm',
    serviceId: 'svc-crm-core',
    email: 'hamza.kettani@kyntus.ma',
  },
  {
    id: 'e-audit-siham',
    firstName: 'Siham',
    lastName: 'Lahlou',
    role: 'Audit',
    poleId: 'pole-apps',
    celluleId: 'cell-crm',
    serviceId: 'svc-crm-core',
    email: 'siham.lahlou@kyntus.ma',
  },
  {
    id: 'e-admin',
    firstName: 'Yassine',
    lastName: 'Touimi',
    role: 'Admin',
    poleId: 'pole-apps',
    celluleId: 'cell-crm',
    serviceId: 'svc-crm-core',
    email: 'yassine.touimi@kyntus.ma',
  },
];

export function demoEmployeeById(id: string): Employee | undefined {
  return DEMO_EMPLOYEES.find((e) => e.id === id);
}

export function demoPilotes(): Employee[] {
  return DEMO_EMPLOYEES.filter((e) => e.role === 'Pilote');
}

export function demoServiceLabel(serviceId: string): string {
  for (const d of DEMO_DEPARTMENTS) {
    for (const p of d.poles) {
      for (const c of p.cells) {
        for (const t of c.teams) {
          if (t.id === serviceId) return t.name;
        }
        if (c.id === serviceId) return c.name;
      }
      if (p.id === serviceId) return p.name;
    }
  }
  return serviceId;
}

export function demoCelluleLabel(celluleId: string): string {
  for (const d of DEMO_DEPARTMENTS) {
    for (const p of d.poles) {
      const c = p.cells.find((x) => x.id === celluleId);
      if (c) return c.name;
    }
  }
  return celluleId;
}

export function demoPoleLabel(poleId: string): string {
  for (const d of DEMO_DEPARTMENTS) {
    const p = d.poles.find((x) => x.id === poleId);
    if (p) return p.name;
  }
  return poleId;
}
