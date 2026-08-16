/** Brouillon local de l'arbre catalogue (aucun appel API tant que non enregistré). */

export type DraftResourceType = 'Pdf' | 'Video' | 'Link' | 'Text' | 'Image';

export interface DraftPendingFile {
  file: File;
  type: 'Pdf' | 'Video' | 'Image';
  /** ClientKey de la ressource créée après upload (ou slot dans la partie). */
  clientKey: string;
  title: string;
  sortOrder: number;
}

export interface DraftPart {
  clientKey: string;
  /** Id de la ressource texte existante (si déjà en base). */
  textResourceId?: string | null;
  title: string;
  textContent: string;
  /** Ressources fichier déjà persistées (conservées au ReplaceStructure). */
  existingFiles: DraftExistingFile[];
  pdfFile: File | null;
  videoFile: File | null;
  imageFile: File | null;
  videoUrl: string;
  linkUrl: string;
  existingVideoId?: string | null;
  existingLinkId?: string | null;
}

export interface DraftExistingFile {
  id: string;
  type: DraftResourceType;
  title: string;
  fileName?: string | null;
  downloadPath?: string | null;
  url?: string | null;
}

export interface DraftLesson {
  clientKey: string;
  id?: string | null;
  title: string;
  description: string;
  isRequired: boolean;
  parts: DraftPart[];
}

export interface DraftModule {
  clientKey: string;
  id?: string | null;
  title: string;
  description: string;
  lessons: DraftLesson[];
}

export function emptyDraftPart(): DraftPart {
  return {
    clientKey: `part_${Math.random().toString(36).slice(2, 10)}`,
    title: '',
    textContent: '',
    existingFiles: [],
    pdfFile: null,
    videoFile: null,
    imageFile: null,
    videoUrl: '',
    linkUrl: '',
  };
}

export function emptyDraftLesson(): DraftLesson {
  return {
    clientKey: `les_${Math.random().toString(36).slice(2, 10)}`,
    title: 'Nouvelle leçon',
    description: '',
    isRequired: true,
    parts: [emptyDraftPart()],
  };
}

export function emptyDraftModule(): DraftModule {
  return {
    clientKey: `mod_${Math.random().toString(36).slice(2, 10)}`,
    title: 'Nouveau module',
    description: '',
    lessons: [emptyDraftLesson()],
  };
}
