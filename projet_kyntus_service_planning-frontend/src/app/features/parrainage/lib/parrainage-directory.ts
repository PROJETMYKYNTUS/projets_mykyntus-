/** Annuaire / libellés orga contact centre (aligné init/contactcentre/roster.json). */

export const MOCK_DEPARTMENTS = [
  { id: 'd1', name: "Relation client & centres d'appels — Casablanca" },
  { id: 'd2', name: 'Support SI & pilotage qualité' },
];

export const MOCK_PROJECTS = [
  { id: 'proj-inbound', name: 'Inbound grands comptes', departmentId: 'd1' },
  { id: 'proj-retention', name: 'Réclamations & rétention', departmentId: 'd1' },
  { id: 'proj-acd', name: 'Supervision connectivité & ACD', departmentId: 'd2' },
  { id: 'proj-pilotage', name: 'Pilotage performance — analyse opérationnelle', departmentId: 'd1' },
];

export const MOCK_ORG_LABELS = {
  departments: {
    d1: "Relation client & centres d'appels — Casablanca",
    d2: 'Support SI & pilotage qualité',
  } as Record<string, string>,
  poles: {
    d1: "Relation client & centres d'appels — Casablanca",
    d2: 'Support SI & pilotage qualité',
  } as Record<string, string>,
  cellules: {
    p1: 'Plateforme inbound — grands comptes',
    p2: 'Réclamations & rétention',
    p3: 'Infrastructure télécom & réseau',
  } as Record<string, string>,
  defaultTeam: 'Équipe contact centre',
} as const;

export interface DirectoryUser {
  id: string;
  name: string;
  email: string;
  projectId?: string;
}

export const MOCK_USERS_BY_ROLE: Record<string, DirectoryUser[]> = {
  PILOTE: [
    { id: '11111111-1111-4111-8111-111111111103', name: 'Yasmine El Idrissi', email: 'employee@kyntus.ma', projectId: 'proj-inbound' },
    { id: '22222222-2222-4222-8222-222222222002', name: 'Mehdi Chraibi', email: 'mehdi.chraibi@contactcentre.ma', projectId: 'proj-inbound' },
    { id: '22222222-2222-4222-8222-222222222004', name: 'Imane Fassi', email: 'imane.fassi@contactcentre.ma', projectId: 'proj-inbound' },
    { id: '33333333-3333-4333-8333-333333333004', name: 'Chaima Benali', email: 'chaima.benali@contactcentre.ma', projectId: 'proj-pilotage' },
    { id: '33333333-3333-4333-8333-333333333005', name: 'Hamid Fellah', email: 'hamid.fellah@contactcentre.ma', projectId: 'proj-pilotage' },
    { id: '33333333-3333-4333-8333-333333333006', name: 'Othmane Kabbaj', email: 'othmane.kabbaj@contactcentre.ma', projectId: 'proj-pilotage' },
  ],
  RH: [
    { id: '11111111-1111-4111-8111-111111111104', name: 'Latifa Mansouri', email: 'rh@kyntus.ma' },
  ],
  ADMIN: [
    { id: '11111111-1111-4111-8111-111111111108', name: 'Système Admin', email: 'admin@kyntus.ma' },
  ],
  COACH: [
    { id: '11111111-1111-4111-8111-111111111106', name: 'Omar Tazi', email: 'coach@kyntus.ma', projectId: 'proj-inbound' },
    { id: '33333333-3333-4333-8333-333333333003', name: 'Younes Elidrissi', email: 'younes.elidrissi@contactcentre.ma', projectId: 'proj-pilotage' },
  ],
  MANAGER: [
    { id: '11111111-1111-4111-8111-111111111105', name: 'Nadia Benchrif', email: 'manager@kyntus.ma', projectId: 'proj-inbound' },
    { id: '11111111-1111-4111-8111-111111111111', name: 'Kenza Alami', email: 'superviseur@kyntus.ma', projectId: 'proj-retention' },
    { id: '33333333-3333-4333-8333-333333333002', name: 'Salim Ouazzani', email: 'salim.ouazzani@contactcentre.ma', projectId: 'proj-pilotage' },
  ],
  RP: [
    { id: '11111111-1111-4111-8111-111111111107', name: 'Ghita Benkirane', email: 'rp@kyntus.ma', projectId: 'proj-inbound' },
    { id: '11111111-1111-4111-8111-111111111110', name: 'Hicham Benjelloun', email: 'formation@kyntus.ma', projectId: 'proj-inbound' },
    { id: '33333333-3333-4333-8333-333333333001', name: 'Malak Souiri', email: 'malak.souiri@contactcentre.ma', projectId: 'proj-pilotage' },
  ],
};
