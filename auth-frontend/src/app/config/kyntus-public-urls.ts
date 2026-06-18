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

export const KYNTUS_PUBLIC_URLS: KyntusPublicUrls =
  (typeof window !== 'undefined' && window.__KYNTUS_PUBLIC_URLS__) || COMPILED_FALLBACK;
