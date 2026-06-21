/** Extrait un message lisible depuis une erreur API PRIME. */
export function allowanceApiErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof Error)) return fallback;
  const raw = error.message.trim();
  if (!raw) return fallback;
  try {
    const parsed = JSON.parse(raw) as { error?: string; message?: string };
    const msg = parsed.error ?? parsed.message;
    if (msg?.includes('42P01')) {
      return 'Service Primes Support indisponible : schéma base de données incomplet. Redémarrez le service PRIME.';
    }
    if (msg) return msg;
  } catch {
    if (raw.includes('42P01')) {
      return 'Service Primes Support indisponible : schéma base de données incomplet. Redémarrez le service PRIME.';
    }
  }
  return raw.length > 200 ? fallback : raw;
}
