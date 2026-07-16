# Contact centre — seeds Formation / Planning / Parrainage

Source de vérité : [`roster.json`](roster.json) (mirroir C# dans chaque service).

## Mapping Auth → persona

| Login Auth | SubjectId | Employé Prime |
|------------|-----------|---------------|
| `employee@kyntus.ma` | `…103` | `e1` Yasmine El Idrissi |
| `coach@kyntus.ma` | `…106` | `e8` Omar Tazi |
| `superviseur@kyntus.ma` | `…111` | `e9` Kenza Alami |
| `manager@kyntus.ma` | `…105` | `e10` Nadia Benchrif |
| `rp@kyntus.ma` | `…107` | `e3` Ghita Benkirane |
| `rh@kyntus.ma` | `…104` | `e5` Latifa Mansouri |
| `audit@kyntus.ma` | `…109` | `e7` Laila Zahidi |
| `admin@kyntus.ma` | `…108` | `e-admin` |
| `formation@kyntus.ma` | `…110` | `e6` Hicham Benjelloun |

Mot de passe démo Docker (défaut compose) : voir `DemoSeed__*` / `Azerty@123`.

## Flags Docker (déjà activés dans `docker-compose.yml`)

| Flag | Service |
|------|---------|
| `KYNTUS_PLANNING_DEMO_SEED=true` | Org + users Planning |
| `KYNTUS_DEMO_ENRICHMENT=true` | Semaines / congés / réclamations (Planning enrichment **v2**) |
| `KYNTUS_FORMATION_DEMO_SEED=true` | Catalogue + TrainingSessions + annuaire |
| `Parrainage__SeedDemoData=true` | 15 parrainages contact centre (**v2**, remplace l’ancien seed « Démo ») |

## Checklist de vérification

Après `docker compose up -d` (ou restart `planning-api`, `formation-api`, `parrainage-api`) :

1. **Planning**
   - Org : Floor « Relation client… », Service « Plateforme inbound », SubServices `c1` / `c2` (plus de « Siège démo »)
   - Users : Yasmine, Mehdi, Omar, Kenza… (noms contact centre)
   - Login `superviseur@kyntus.ma` → plannings publiés sur `c1`, notifications
2. **Formation**
   - Liste sessions : softphone, rétention, NPS, ACD…
   - Login `employee@kyntus.ma` → « mes sessions » non vides
   - Login `formation@kyntus.ma` → sessions animées (Hicham)
3. **Parrainage**
   - Admin : parrains Yasmine / Omar / Kenza / Ghita / Nadia (plus de « Employé Démo »)
   - Login `employee@kyntus.ma` → « mes parrainages » (ReferrerId = SubjectId `…103`)

## Re-seed sur base déjà peuplée

- **Planning enrichment** : marqueur `DockerPlanningEnrichmentV2` — redémarre le service ; les semaines déjà créées sont conservées.
- **Formation TrainingSessions** : marqueur titre `Qualité softphone — Agents 1er niveau (contact centre)`.
- **Parrainage** : si d’anciens referrals « … Démo » / `kyntus-*` existent, ils sont **remplacés** au démarrage.
- Sinon : `docker compose down -v` puis `up` pour une base vierge.
