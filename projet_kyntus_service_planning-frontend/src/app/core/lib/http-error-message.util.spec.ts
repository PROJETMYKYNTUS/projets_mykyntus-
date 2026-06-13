import { describe, expect, it } from 'vitest';
import { HttpErrorResponse } from '@angular/common/http';
import { formatHttpErrorMessage } from './http-error-message.util';
import { resolveUserGuid } from './user-guid.util';

describe('http-error-message.util', () => {
  it('reads Prime API error field', () => {
    const err = new HttpErrorResponse({
      status: 404,
      error: { error: 'Employé introuvable.' },
    });
    expect(formatHttpErrorMessage(err)).toBe('Employé introuvable.');
  });

  it('reads planning message field', () => {
    const err = new HttpErrorResponse({
      status: 400,
      error: { message: "L'adresse email est déjà utilisée." },
    });
    expect(formatHttpErrorMessage(err)).toBe("L'adresse email est déjà utilisée.");
  });

  it('reads plain Error objects', () => {
    expect(formatHttpErrorMessage(new Error('Identifiant employé Prime manquant.'))).toBe(
      'Identifiant employé Prime manquant.',
    );
  });
});

describe('user-guid.util', () => {
  it('resolves guid from camelCase or PascalCase', () => {
    expect(resolveUserGuid({ guid: 'abc' })).toBe('abc');
    expect(resolveUserGuid({ guid: '', Guid: 'def' })).toBe('def');
  });
});
