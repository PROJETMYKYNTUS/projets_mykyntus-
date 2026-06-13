import type { User } from '../../features/users/users-module';

type UserGuidSource = Pick<User, 'guid'> & { Guid?: string };

export function resolveUserGuid(user: UserGuidSource | null | undefined): string {
  if (!user) return '';
  return String(user.guid ?? user.Guid ?? '').trim();
}
