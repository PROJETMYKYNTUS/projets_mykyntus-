/** Contexte navigation Prime pour un JWT Manager titulaire d’un département métier. */
export type PrimeDepartmentManagerNav = {
  isSupportManager: boolean;
  isOperationalManager: boolean;
};

export function buildPrimeDepartmentManagerNav(input: {
  isSupportManager(): boolean;
  isOperationalManager(): boolean;
}): PrimeDepartmentManagerNav {
  return {
    isSupportManager: input.isSupportManager(),
    isOperationalManager: input.isOperationalManager(),
  };
}
