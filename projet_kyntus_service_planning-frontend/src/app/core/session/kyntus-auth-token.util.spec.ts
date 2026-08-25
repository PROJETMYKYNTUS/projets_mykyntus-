import { describe, expect, it } from 'vitest';
import { decodeJwtPayload, readJwtRole, readJwtRoles } from './kyntus-auth-token.util';
import { KYNTUS_JWT_CLAIMS } from './kyntus-session.constants';

function makeToken(payload: Record<string, unknown>): string {
  const json = JSON.stringify(payload);
  const b64 = btoa(json).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '');
  return `hdr.${b64}.sig`;
}

describe('readJwtRole', () => {
  it('lit le claim court role (JwtSecurityTokenHandler outbound)', () => {
    const token = makeToken({ role: 'Superviseur', exp: 4102444800 });
    expect(readJwtRole(token)).toBe('Superviseur');
  });

  it('lit l’URI Microsoft si présente', () => {
    const token = makeToken({ [KYNTUS_JWT_CLAIMS.role]: 'Admin' });
    expect(readJwtRole(token)).toBe('Admin');
  });

  it('lit un tableau roles', () => {
    const token = makeToken({ roles: ['RH', 'Admin'] });
    expect(readJwtRoles(token)).toEqual(['RH', 'Admin']);
    expect(readJwtRole(token)).toBe('RH');
  });

  it('décode le payload base64url', () => {
    const token = makeToken({ role: 'Pilote' });
    expect(decodeJwtPayload(token)?.['role']).toBe('Pilote');
  });
});
