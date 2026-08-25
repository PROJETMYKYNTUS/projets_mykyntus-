# Delta déploiement serveur

À partir du **2026-08-20**, on ne recopie plus tout le dépôt : on suit les fichiers touchés puis on exporte un tar.

## Fichiers

| Fichier | Rôle |
|---------|------|
| `last-deploy.json` | Dernier mark réussi (date, commit, hôte) |
| `pending-files.txt` | Chemins à déployer depuis ce mark |
| `history.jsonl` | Historique des marks / baselines |
| `out/` | Archives tar générées (peut être ignoré par git) |

## Commandes

```powershell
# Voir ce qui est en attente
.\scripts\deploy-delta.ps1 -Action status

# Après un correctif local
.\scripts\deploy-delta.ps1 -Action add -Path 'PlanningService\...\Fichier.cs','docker-compose.yml'

# Optionnel : aussi tout ce que git a changé depuis le mark
.\scripts\deploy-delta.ps1 -Action status -IncludeGitDiff
.\scripts\deploy-delta.ps1 -Action export -IncludeGitDiff

# Générer tar + afficher scp/ssh
.\scripts\deploy-delta.ps1 -Action export

# Après déploiement + rebuild OK sur le serveur
.\scripts\deploy-delta.ps1 -Action mark -Note 'fix X'
```

## Règle agent / équipe

Après toute modification destinée au serveur, ajouter le chemin dans `pending-files.txt` (via `-Action add` ou à la main). Ne pas marquer (`mark`) avant que le patch soit réellement sur le serveur.
