# Pôle pilotage performance — seeds métier

Source : [`roster.json`](roster.json)

## Collaborateurs

| Nom | Rôle | Email |
|-----|------|-------|
| Malak Souiri | Chef de projet | malak.souiri@contactcentre.ma |
| Salim Ouazzani | Superviseur | salim.ouazzani@contactcentre.ma |
| Younes Elidrissi | Référent technique | younes.elidrissi@contactcentre.ma |
| Chaima Benali, Hamid Fellah, Othmane Kabbaj, Asmae Tazi, Rania Karimi | Pilotes | `*.@contactcentre.ma` |

GUIDs stables `33333333-…` (Directory = Planning `User.Guid` = Formation = Parrainage `ReferrerId`).

## Ce qui est peuplé

| Module | Contenu |
|--------|---------|
| **Directory** | OP-002, pôle / cellule suivi KPI / service analyse opérationnelle + affectations |
| **Planning** | Users + 4 semaines (3 publiées) + congés + réclamations sur ANALYSE-OP |
| **Formation** | Annuaire + 3 TrainingSessions (Scheduled / InProgress / Completed) + inscriptions |
| **Parrainage** | 8 referrals `ref-pilot-*` (projet Pilotage performance) |

## Activation

Flags déjà présents dans `docker-compose.yml` :

- `KYNTUS_DIRECTORY_DEMO_SEED` / `KYNTUS_DEMO_ENRICHMENT`
- `KYNTUS_PLANNING_DEMO_SEED` + `KYNTUS_DEMO_ENRICHMENT`
- `KYNTUS_FORMATION_DEMO_SEED`
- `Parrainage__SeedDemoData`

Redémarrer : `employee-directory`, `planning-api`, `formation-api`, `parrainage-api`.

## Vérification UI

1. **Organisation RH** — pôle pilotage performance + membres
2. **Planification** — cellule « service analyse operationnelle », plannings Younes / Chaima / …
3. **Formation** — sessions « Indicateurs KPI… », « Analyse opérationnelle… »
4. **Parrainage** — parrains Chaima, Hamid, Malak, Salim… projet Pilotage performance
