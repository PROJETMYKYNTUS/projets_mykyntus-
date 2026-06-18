import { describe, expect, it } from 'vitest';

import { buildSampleValuesFromVariables, sampleValueForVariableName } from './documentation-template-sample.util';
import type { TemplateVariableDto } from '../models/documentation.models';

describe('documentation-template-sample.util', () => {
  it('sampleValueForVariableName returns CIN format', () => {
    expect(sampleValueForVariableName('numero_cin')).toBe('AB123456');
  });

  it('sampleValueForVariableName returns French date format', () => {
    expect(sampleValueForVariableName('date_de_travail')).toBe('13/06/2024');
  });

  it('buildSampleValuesFromVariables maps all variable names', () => {
    const vars: TemplateVariableDto[] = [
      {
        id: '1',
        name: 'nom',
        type: 'text',
        isRequired: true,
        defaultValue: null,
        validationRule: null,
        formScope: 'db',
        sortOrder: 0,
      },
      {
        id: '2',
        name: 'cin',
        type: 'text',
        isRequired: true,
        defaultValue: null,
        validationRule: null,
        formScope: 'pilot',
        sortOrder: 1,
      },
    ];
    const values = buildSampleValuesFromVariables(vars);
    expect(values['nom']).toBe('Alaoui');
    expect(values['cin']).toBe('AB123456');
  });
});
