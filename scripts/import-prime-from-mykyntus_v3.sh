#!/usr/bin/env bash
# Importe le module Prime depuis origin/mykyntus_v3 sur la branche courante (mykyntus_v2).
# Usage (Git Bash, à la racine du dépôt) :
#   bash scripts/import-prime-from-mykyntus_v3.sh

set -euo pipefail
cd "$(dirname "$0")/.."

REMOTE="${REMOTE:-origin}"
BRANCH="${BRANCH:-mykyntus_v3}"
REF="${REMOTE}/${BRANCH}"

echo "Dépôt : $(pwd)"
echo "Branche courante : $(git branch --show-current)"

git fetch "$REMOTE" "$BRANCH"

# Une seule commande — pas de backticks ` (réservés à la substitution bash).
git checkout "$REF" -- \
  PrimeBackend \
  prime-angular \
  docs/prime-fiche-template-v1.md \
  docs/prime-fiche-template-v2.md \
  docs/prime-manual-test-checklist.md \
  docs/prime-validation-api-scope.md \
  init/sql/prime_database.sql \
  docker-compose.yml

echo ""
echo "OK. Vérifiez : git status && git diff --stat"
