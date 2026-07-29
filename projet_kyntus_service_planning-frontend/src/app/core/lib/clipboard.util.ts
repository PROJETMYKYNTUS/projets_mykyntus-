/**
 * Copie du texte vers le presse-papiers avec repli execCommand
 * (Clipboard API peut échouer hors contexte sécurisé / permission refusée).
 */
export async function copyTextToClipboard(text: string): Promise<void> {
  const value = text ?? '';
  if (!value) {
    throw new Error('Aucun texte à copier.');
  }

  if (typeof navigator !== 'undefined' && navigator.clipboard?.writeText) {
    try {
      await navigator.clipboard.writeText(value);
      return;
    } catch {
      // fallback below
    }
  }

  const ta = document.createElement('textarea');
  ta.value = value;
  ta.setAttribute('readonly', '');
  ta.style.position = 'fixed';
  ta.style.left = '-9999px';
  ta.style.top = '0';
  document.body.appendChild(ta);
  ta.focus();
  ta.select();
  ta.setSelectionRange(0, value.length);

  let ok = false;
  try {
    ok = document.execCommand('copy');
  } finally {
    document.body.removeChild(ta);
  }

  if (!ok) {
    throw new Error('Copie presse-papiers impossible.');
  }
}
