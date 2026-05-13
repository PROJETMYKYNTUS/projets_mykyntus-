# Échantillons — Fiche PRIME

## Fichier exemplaire

Copiez votre classeur **« Exemplaire Prime »** ici sous le nom :

- `Exemplaire-Prime.xlsx` (layout **v1**, voir [../prime-fiche-template-v1.md](../prime-fiche-template-v1.md))

Référence **v2** (layout exemplaire actuel) : `EXEMPLAIRE PRIME1.xlsx` — voir [../prime-fiche-template-v2.md](../prime-fiche-template-v2.md).

Exemple (PowerShell, depuis la racine du dépôt) :

```powershell
Copy-Item "C:\chemin\vers\Exemplaire Prime.xlsx" "docs\samples\Exemplaire-Prime.xlsx"
```

Le dépôt peut fonctionner sans ce fichier (tests unitaires génèrent un classeur minimal en mémoire). Le fichier sert de **référence humaine** alignée sur [../prime-fiche-template-v1.md](../prime-fiche-template-v1.md).

À l’import dans « Templates fiche PRIME », l’application extrait en plus une copie **multi-feuilles** bornée (`calcSheets`) pour recalculer les formules dans le navigateur (aperçu / résultat), y compris les références vers d’autres feuilles du classeur exemplaire. Les templates déjà enregistrés **avant** cette fonctionnalité n’ont pas ce bloc : **réimportez** le `.xlsx` puis enregistrez à nouveau le template pour bénéficier du recalcul complet.
