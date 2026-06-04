import {
  AlertTriangle,
  Bell,
  Calendar,
  CheckCircle,
  ClipboardList,
  Eye,
  FileText,
  Info,
  Pencil,
  Rocket,
  Settings,
  Trash2,
  type IconNode,
} from 'lucide';

/** Icônes sémantiques pour remplacer les emojis dans l’UI planning. */
export const UI_ICONS = {
  bell: Bell,
  calendar: Calendar,
  check: CheckCircle,
  warning: AlertTriangle,
  info: Info,
  trash: Trash2,
  eye: Eye,
  edit: Pencil,
  rocket: Rocket,
  settings: Settings,
  clipboard: ClipboardList,
  file: FileText,
} as const satisfies Record<string, IconNode>;
