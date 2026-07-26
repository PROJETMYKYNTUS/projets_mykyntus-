import type { KyntusTheme } from './kyntus-theme.service';

/** Logos brand — icône seule, fond transparent (public/images/brand/) */
export const BRAND_LOGO_LIGHT = 'images/brand/logo-mode-claire.png?v=icon3';
export const BRAND_LOGO_DARK = 'images/brand/logo-mode-sombre.png?v=icon3';

/**
 * Logo adapté au thème de page.
 * Pour un panneau toujours sombre (ex. login gauche), passer `forceDarkPanel: true`.
 */
export function brandLogoSrc(theme: KyntusTheme, options?: { forceDarkPanel?: boolean }): string {
  if (options?.forceDarkPanel) return BRAND_LOGO_DARK;
  return theme === 'dark' ? BRAND_LOGO_DARK : BRAND_LOGO_LIGHT;
}
