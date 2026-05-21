import type { PrimeNotification, PrimeNotificationType } from '../models/notification.model';

function daysAgo(n: number): Date {
  const d = new Date();
  d.setDate(d.getDate() - n);
  return d;
}

export const PrimeNotificationService = {
  seed(): PrimeNotification[] {
    return [
      { id: 1, type: 'primeValidated', createdAt: daysAgo(0), read: false },
      { id: 2, type: 'teamPerformanceUpdated', createdAt: daysAgo(1), read: false },
      { id: 3, type: 'newPrimeRule', createdAt: daysAgo(2), read: false },
      { id: 4, type: 'primeRejected', createdAt: daysAgo(3), read: true },
      { id: 5, type: 'primeValidated', createdAt: daysAgo(5), read: true },
      { id: 6, type: 'teamPerformanceUpdated', createdAt: daysAgo(7), read: true },
    ];
  },

  push(prev: PrimeNotification[], type: PrimeNotificationType): PrimeNotification[] {
    const nextId = prev.length ? prev[0].id + 1 : 1;
    return [{ id: nextId, type, createdAt: new Date(), read: false }, ...prev];
  },

  markAllAsRead(prev: PrimeNotification[]): PrimeNotification[] {
    return prev.map((n) => ({ ...n, read: true }));
  },

  markAsRead(prev: PrimeNotification[], id: number): PrimeNotification[] {
    return prev.map((n) => (n.id === id ? { ...n, read: true } : n));
  },

  unreadCount(prev: PrimeNotification[]): number {
    return prev.filter((n) => !n.read).length;
  },
};

