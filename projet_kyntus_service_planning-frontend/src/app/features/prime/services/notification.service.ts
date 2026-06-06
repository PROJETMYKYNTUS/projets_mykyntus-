import type { PrimeNotification, PrimeNotificationType } from '../models/notification.model';

const STORAGE_KEY = 'kyntus_prime_notifications';

function load(): PrimeNotification[] {
  if (typeof localStorage === 'undefined') return [];
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw) as Array<{ id: number; type: PrimeNotificationType; createdAt: string; read: boolean }>;
    if (!Array.isArray(parsed)) return [];
    return parsed.map((n) => ({
      id: n.id,
      type: n.type,
      read: n.read,
      createdAt: new Date(n.createdAt),
    }));
  } catch {
    return [];
  }
}

function persist(list: PrimeNotification[]): void {
  localStorage.setItem(
    STORAGE_KEY,
    JSON.stringify(list.map((n) => ({ ...n, createdAt: n.createdAt.toISOString() }))),
  );
}

export const PrimeNotificationService = {
  load(): PrimeNotification[] {
    return load();
  },

  push(prev: PrimeNotification[], type: PrimeNotificationType): PrimeNotification[] {
    const nextId = prev.length ? Math.max(...prev.map((n) => n.id)) + 1 : 1;
    const next = [{ id: nextId, type, createdAt: new Date(), read: false }, ...prev];
    persist(next);
    return next;
  },

  markAllAsRead(prev: PrimeNotification[]): PrimeNotification[] {
    const next = prev.map((n) => ({ ...n, read: true }));
    persist(next);
    return next;
  },

  markAsRead(prev: PrimeNotification[], id: number): PrimeNotification[] {
    const next = prev.map((n) => (n.id === id ? { ...n, read: true } : n));
    persist(next);
    return next;
  },

  unreadCount(prev: PrimeNotification[]): number {
    return prev.filter((n) => !n.read).length;
  },
};
