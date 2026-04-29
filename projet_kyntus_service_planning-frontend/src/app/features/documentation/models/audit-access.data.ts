export interface AuditAccessRow {

  id: string;

  user: string;

  datetime: string;

  ip: string;

  location: string;

  success: boolean;

  /** LibellÃ© technique issu du journal dâ€™audit (API). */

  type: string;

  role: string;

  departement: string;

}

