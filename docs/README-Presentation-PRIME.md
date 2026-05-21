# Présentation Module PRIME (managers)

## Obtenir le fichier PowerPoint

### Méthode recommandée (sans installation)

1. Ouvrir **`Module-PRIME-Generer-Presentation.html`** dans **Chrome** ou **Edge** (double-clic).
2. Cliquer sur **« Télécharger la présentation (.pptx) »**.
3. Le fichier **`Module-PRIME-Presentation-Managers.pptx`** est enregistré dans votre dossier Téléchargements (déplacez-le dans `docs/` si besoin).

> Connexion Internet requise une seule fois (chargement de la bibliothèque pptxgenjs).

### Méthode alternative (Python)

```powershell
cd docs
py -3 -m pip install python-pptx pillow
py -3 generate_prime_presentation.py
```

Le fichier est créé ici : `docs/Module-PRIME-Presentation-Managers.pptx`

## Contenu livré

| Fichier | Rôle |
|---------|------|
| `Module-PRIME-Generer-Presentation.html` | Générateur navigateur → .pptx |
| `generate_prime_presentation.py` | Générateur Python (même contenu) |
| `Guide-Presentation-PRIME-7min.md` | Script oral, timing, FAQ |
| `Module-PRIME-Presentation-Managers.pptx` | À générer (voir ci-dessus) |

## Personnalisation avant présentation

- Slide 1 : logo, entreprise, équipe, date
- Slides 3, 5, 6, 9 : remplacer **INSÉRER CAPTURE ICI** par vos captures d’écran
- Slide 4 : valider ou retirer les pourcentages illustratifs
- Slide 11 : nom et contact du présentateur

## Structure (11 slides · ~7 min)

1. Page de garde  
2. Problématique actuelle  
3. La solution PRIME  
4. Gains entreprise  
5. Gains managers  
6. Automatisation intelligente  
7. Flexibilité & évolutivité  
8. Vision future  
9. Aperçu interfaces  
10. Conclusion  
11. Merci / Questions  

Voir **`Guide-Presentation-PRIME-7min.md`** pour le script détaillé.
