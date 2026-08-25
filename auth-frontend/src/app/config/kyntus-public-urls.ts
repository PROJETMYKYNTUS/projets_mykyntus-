export interface KyntusPublicUrls {
  host: string;
  gateway: string;
  planningSpa: string;
  authFrontend: string;
  authLogin: string;
  planningAuthCallback: string;
}

declare global {
  interface Window {
    __KYNTUS_PUBLIC_URLS__?: KyntusPublicUrls;
  }
}

/** Fallback si kyntus-public-urls.js absent (ex. ng serve sans script) */
const COMPILED_FALLBACK: KyntusPublicUrls = {
  host: 'localhost',
  gateway: 'http://localhost:8500',
  planningSpa: 'http://localhost:8200',
  authFrontend: 'http://localhost:8201',
  authLogin: 'http://localhost:8201/login',
  planningAuthCallback: 'http://localhost:8200/auth-callback',
};

/** Lit window.__KYNTUS_PUBLIC_URLS__ à chaque appel (évite un profil figé au import). */
export function getKyntusPublicUrls(): KyntusPublicUrls {
  if (typeof window !== 'undefined' && window.__KYNTUS_PUBLIC_URLS__) {
    return window.__KYNTUS_PUBLIC_URLS__;
  }
  return COMPILED_FALLBACK;
}

/** Accès propriété dynamique — préférer getKyntusPublicUrls() dans le nouveau code. */
export const KYNTUS_PUBLIC_URLS: KyntusPublicUrls = new Proxy({} as KyntusPublicUrls, {
  get(_target, prop: string | symbol) {
    const urls = getKyntusPublicUrls();
    if (typeof prop === 'string' && prop in urls) {
      return urls[prop as keyof KyntusPublicUrls];
    }
    return undefined;
  },
});
