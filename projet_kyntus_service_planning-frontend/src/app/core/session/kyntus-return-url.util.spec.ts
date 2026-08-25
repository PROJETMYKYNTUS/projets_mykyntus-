import { beforeEach, describe, expect, it } from 'vitest';
import {
  sanitizeReturnUrl,
  resolveReturnUrl,
  KYNTUS_RETURN_URL_KEY,
} from './kyntus-return-url.util';

describe('kyntus-return-url.util', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it('accepte un chemin SPA relatif', () => {
    expect(sanitizeReturnUrl('/planning/generate?x=1')).toBe('/planning/generate?x=1');
  });

  it('rejette les URLs externes et protocol-relative', () => {
    expect(sanitizeReturnUrl('https://evil.com')).toBeNull();
    expect(sanitizeReturnUrl('//evil.com')).toBeNull();
    expect(sanitizeReturnUrl('javascript:alert(1)')).toBeNull();
  });

  it('rejette auth-callback, login et unauthorized', () => {
    expect(sanitizeReturnUrl('/auth-callback')).toBeNull();
    expect(sanitizeReturnUrl('/login')).toBeNull();
    expect(sanitizeReturnUrl('/unauthorized')).toBeNull();
  });

  it('resolveReturnUrl préfère la query puis sessionStorage', () => {
    sessionStorage.setItem(KYNTUS_RETURN_URL_KEY, '/home');
    expect(resolveReturnUrl('/shift-config')).toBe('/shift-config');
    expect(resolveReturnUrl(null)).toBe('/home');
  });
});
