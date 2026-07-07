/** Claims JWT émis par AuthService (alignés sur auth-callback). */
export const KYNTUS_JWT_CLAIMS = {
  role: 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role',
  nameIdentifier: 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier',
  name: 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name',
  email: 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress',
} as const;

export const KYNTUS_DEFAULT_TENANT = 'atlas-tech-demo';

export interface KyntusStoredUser {
  /** SubjectId Auth (UUID) — legacy champ `id`. */
  id: number | string;
  authUserId?: number;
  subjectId?: string;
  username: string;
  email: string;
  role: string;
}
