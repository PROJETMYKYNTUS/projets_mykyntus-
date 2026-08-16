import type { User } from '../../features/users/users-module';

type UserGuidSource = Pick<User, 'guid'> & { Guid?: string };

export function resolveUserGuid(user: UserGuidSource | null | undefined): string {
  if (!user) return '';
  return String(user.guid ?? user.Guid ?? '').trim();
}

type StoredUser = {
  subjectId?: string;
  guid?: string;
  Guid?: string;
  id?: string | number;
};

/**
 * GUID acteur pour les appels métier : priorité JWT subjectId → guid Planning → id dashed.
 * Les listes « Mes formations » s’appuient surtout sur le JWT côté API ; ce helper reste utile
 * pour les écrans qui passent encore un employeeId explicite.
 */
export function resolveCurrentUserGuid(): string {
  try {
    const raw = localStorage.getItem('user');
    if (!raw) return '';
    const user = JSON.parse(raw) as StoredUser;
    const fromSubject = String(user.subjectId ?? '').trim();
    if (fromSubject.includes('-')) return fromSubject;
    const fromGuid = String(user.guid ?? user.Guid ?? '').trim();
    if (fromGuid.includes('-')) return fromGuid;
    const fromId = String(user.id ?? '').trim();
    if (fromId.includes('-')) return fromId;
    return '';
  } catch {
    return '';
  }
}
