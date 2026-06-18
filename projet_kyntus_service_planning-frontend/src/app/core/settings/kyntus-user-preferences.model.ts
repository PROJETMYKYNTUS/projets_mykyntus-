export interface KyntusNotificationPreferences {
  planning: boolean;
  contracts: boolean;
  reclamations: boolean;
  propositions: boolean;
  prime: boolean;
  parrainage: boolean;
  documentation: boolean;
  formation: boolean;
  conge: boolean;
  newsletter: boolean;
}

export const DEFAULT_NOTIFICATION_PREFERENCES: KyntusNotificationPreferences = {
  planning: true,
  contracts: true,
  reclamations: true,
  propositions: true,
  prime: true,
  parrainage: true,
  documentation: true,
  formation: true,
  conge: true,
  newsletter: true,
};

export const KYNTHUS_NOTIFICATION_PREFS_KEY = 'kyntus_notification_prefs';

export interface KyntusUserPreferences {
  compactMode: boolean;
  notifications: KyntusNotificationPreferences;
}

export const DEFAULT_USER_PREFERENCES: KyntusUserPreferences = {
  compactMode: false,
  notifications: { ...DEFAULT_NOTIFICATION_PREFERENCES },
};
