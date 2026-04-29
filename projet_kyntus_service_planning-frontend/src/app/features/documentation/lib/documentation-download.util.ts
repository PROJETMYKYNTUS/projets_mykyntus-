import { HttpErrorResponse, HttpResponse } from '@angular/common/http';

/** TÃ©lÃ©chargement fiable (certains navigateurs exigent lâ€™Ã©lÃ©ment dans le DOM). */
export function triggerBlobDownload(blob: Blob, fileName: string): void {
  const safeName = fileName.replace(/[/\\?%*:|"<>]/g, '-');
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = safeName;
  a.rel = 'noopener';
  a.style.display = 'none';
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

/** @deprecated Utiliser {@link triggerBlobDownload} (mÃªme comportement). */
export function triggerPdfDownload(blob: Blob, fileName: string): void {
  triggerBlobDownload(blob, fileName);
}

/** Extrait le nom de fichier depuis lâ€™en-tÃªte Content-Disposition (filename / RFC 5987). */
export function fileNameFromContentDisposition(header: string | null | undefined): string | null {
  if (!header?.trim()) return null;
  const star = /filename\*=(?:UTF-8'')?([^;]+)/i.exec(header);
  if (star) {
    const raw = star[1].trim().replace(/^"|"$/g, '');
    try {
      return decodeURIComponent(raw);
    } catch {
      return raw;
    }
  }
  const q = /filename="([^"]+)"/i.exec(header);
  if (q) return q[1];
  const p = /filename=([^;\s]+)/i.exec(header);
  if (p) return p[1].replace(/^"|"$/g, '');
  return null;
}

export function triggerDownloadFromHttpResponse(resp: HttpResponse<Blob>, fallbackFileName: string): void {
  const body = resp.body;
  if (!body) return;
  const fromHeader = fileNameFromContentDisposition(resp.headers.get('Content-Disposition'));
  triggerBlobDownload(body, fromHeader ?? fallbackFileName);
}

/** Ouvre un PDF dans un nouvel onglet (type MIME explicite). */
export function openPdfInNewTab(blob: Blob): string {
  const pdfBlob = new Blob([blob], { type: 'application/pdf' });
  const url = URL.createObjectURL(pdfBlob);
  window.open(url, '_blank', 'noopener');
  return url;
}

export async function formatDocumentationHttpError(e: unknown): Promise<string> {
  if (!(e instanceof HttpErrorResponse)) {
    return 'Erreur rÃ©seau ou serveur.';
  }
  if (e.error instanceof Blob) {
    try {
      const text = await e.error.text();
      try {
        const j = JSON.parse(text) as { message?: string };
        if (j?.message) {
          return j.message;
        }
      } catch {
        /* pas du JSON */
      }
      return text.trim().slice(0, 300) || e.message || 'TÃ©lÃ©chargement refusÃ©.';
    } catch {
      return e.message || 'TÃ©lÃ©chargement refusÃ©.';
    }
  }
  const body = e.error;
  if (body && typeof body === 'object' && 'message' in body) {
    return String((body as { message?: string }).message);
  }
  return e.message || 'Action refusÃ©e par le serveur.';
}
