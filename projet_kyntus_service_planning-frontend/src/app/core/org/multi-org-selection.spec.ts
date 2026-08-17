import { describe, expect, it } from 'vitest';
import {
  addOrgNodeSelection,
  clearMultiOrgSelection,
  hydrateMultiOrgSelectionFromOverview,
  removeOrgNodeSelection,
  setPrimaryOrgNode,
  supportsMultiOrgSelection,
  validateMultiOrgSelection,
} from './multi-org-selection';
import type { OrgAssignmentsOverview } from '../../features/prime/services/prime-org-api.service';

describe('multi-org-selection', () => {
  it('supports multi for chef/superviseur/rt only', () => {
    expect(supportsMultiOrgSelection('Chef de projet')).toBe(true);
    expect(supportsMultiOrgSelection('Superviseur')).toBe(true);
    expect(supportsMultiOrgSelection('Référent technique')).toBe(true);
    expect(supportsMultiOrgSelection('Pilote')).toBe(false);
    expect(supportsMultiOrgSelection('Manager')).toBe(false);
  });

  it('adds without duplicates and sets first as primary', () => {
    let state = clearMultiOrgSelection();
    state = addOrgNodeSelection(state, 'pole-a');
    expect(state).toEqual({ selectedOrgNodeIds: ['pole-a'], primaryOrgNodeId: 'pole-a' });
    state = addOrgNodeSelection(state, 'pole-b');
    expect(state.selectedOrgNodeIds).toEqual(['pole-a', 'pole-b']);
    expect(state.primaryOrgNodeId).toBe('pole-a');
    state = addOrgNodeSelection(state, 'pole-a');
    expect(state.selectedOrgNodeIds).toEqual(['pole-a', 'pole-b']);
  });

  it('removes and reassigns primary', () => {
    let state = {
      selectedOrgNodeIds: ['pole-a', 'pole-b'],
      primaryOrgNodeId: 'pole-a',
    };
    state = removeOrgNodeSelection(state, 'pole-a');
    expect(state).toEqual({ selectedOrgNodeIds: ['pole-b'], primaryOrgNodeId: 'pole-b' });
  });

  it('changes primary only when included', () => {
    const state = {
      selectedOrgNodeIds: ['pole-a', 'pole-b'],
      primaryOrgNodeId: 'pole-a',
    };
    expect(setPrimaryOrgNode(state, 'pole-b').primaryOrgNodeId).toBe('pole-b');
    expect(setPrimaryOrgNode(state, 'pole-x').primaryOrgNodeId).toBe('pole-a');
  });

  it('hydrates multiple chef poles and primary from employee', () => {
    const overview = {
      employees: [
        {
          id: 'u1',
          firstName: 'A',
          lastName: 'B',
          role: 'Chef de projet',
          poleId: 'pole-b',
          celluleId: '',
          serviceId: '',
          email: 'a@test.com',
        },
      ],
      managerEtage: [
        { id: '1', userId: 'u1', etageId: 'pole-a' },
        { id: '2', userId: 'u1', etageId: 'pole-b' },
      ],
      supervisorService: [],
      coachSousService: [],
    } as unknown as OrgAssignmentsOverview;

    expect(hydrateMultiOrgSelectionFromOverview(overview, 'u1', 'Chef de projet')).toEqual({
      selectedOrgNodeIds: ['pole-a', 'pole-b'],
      primaryOrgNodeId: 'pole-b',
    });
  });

  it('validates empty and selection coherence', () => {
    expect(validateMultiOrgSelection(clearMultiOrgSelection(), true)).toMatch(/au moins/);
    expect(
      validateMultiOrgSelection(
        { selectedOrgNodeIds: ['a'], primaryOrgNodeId: 'b' },
        true,
      ),
    ).toMatch(/incohérente/);
    expect(
      validateMultiOrgSelection(
        { selectedOrgNodeIds: ['a'], primaryOrgNodeId: 'a' },
        true,
      ),
    ).toBeNull();
  });
});
