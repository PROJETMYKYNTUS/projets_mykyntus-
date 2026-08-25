/**
 * Fenêtre de modification d'une évaluation : cycle "du 15 au 14 du mois suivant".
 *
 * Règle (en accord avec backend, score.routes.js -> PATCH /scores/:id) :
 *  - Si aujourd'hui >= 15 du mois courant : cycle = [15 du mois courant, 15 du mois suivant[
 *  - Sinon                                : cycle = [15 du mois précédent, 15 du mois courant[
 *  - Une évaluation est modifiable si son createdAt est dans le cycle courant.
 *
 * @param {Date|string|number} dateRef date de référence (ex. score.createdAt)
 * @returns {boolean}
 */
export function isInCurrentEditCycle(dateRef) {
  if (!dateRef) return false;
  const d = new Date(dateRef);
  if (Number.isNaN(d.getTime())) return false;

  const now = new Date();
  const cycleStart = (() => {
    if (now.getDate() >= 15) {
      return new Date(now.getFullYear(), now.getMonth(), 15, 0, 0, 0, 0);
    }
    return new Date(now.getFullYear(), now.getMonth() - 1, 15, 0, 0, 0, 0);
  })();
  const cycleEnd = new Date(
    cycleStart.getFullYear(),
    cycleStart.getMonth() + 1,
    15,
    0, 0, 0, 0
  );

  return d >= cycleStart && d < cycleEnd;
}

