import { DomSanitizer, SafeHtml, SafeResourceUrl } from '@angular/platform-browser';
import type { TrainingResourceDto } from '../models/formation-training.models';

const ALLOWED_TAGS = new Set([
  'p', 'br', 'ul', 'ol', 'li', 'strong', 'b', 'em', 'i', 'u', 'a', 'img',
  'h2', 'h3', 'h4', 'span', 'div',
]);

const ALLOWED_ATTRS: Record<string, Set<string>> = {
  a: new Set(['href', 'title', 'target', 'rel']),
  img: new Set(['src', 'alt', 'title', 'width', 'height']),
  '*': new Set(['class']),
};

/** Sanitize HTML for catalog text resources (allowlist). */
export function sanitizeLearningHtml(raw: string | null | undefined): string {
  if (!raw?.trim()) return '';
  if (typeof DOMParser === 'undefined') {
    return raw.replace(/</g, '&lt;');
  }
  const doc = new DOMParser().parseFromString(`<div>${raw}</div>`, 'text/html');
  const root = doc.body.firstElementChild;
  if (!root) return '';
  walkSanitize(root);
  return root.innerHTML;
}

function walkSanitize(el: Element): void {
  const children = Array.from(el.childNodes);
  for (const node of children) {
    if (node.nodeType === Node.ELEMENT_NODE) {
      const child = node as Element;
      const tag = child.tagName.toLowerCase();
      if (!ALLOWED_TAGS.has(tag)) {
        // Unwrap: keep children, remove tag
        while (child.firstChild) {
          el.insertBefore(child.firstChild, child);
        }
        el.removeChild(child);
        continue;
      }
      // Strip disallowed attrs
      for (const attr of Array.from(child.attributes)) {
        const name = attr.name.toLowerCase();
        const allowed =
          ALLOWED_ATTRS[tag]?.has(name) || ALLOWED_ATTRS['*']?.has(name);
        if (!allowed) {
          child.removeAttribute(attr.name);
          continue;
        }
        if (name === 'href' || name === 'src') {
          const v = attr.value.trim();
          if (/^javascript:/i.test(v) || /^data:text\/html/i.test(v)) {
            child.removeAttribute(attr.name);
          }
        }
        if (name === 'target') {
          child.setAttribute('rel', 'noopener noreferrer');
        }
      }
      walkSanitize(child);
    } else if (node.nodeType === Node.COMMENT_NODE) {
      el.removeChild(node);
    }
  }
}

export function trustLearningHtml(sanitizer: DomSanitizer, raw: string | null | undefined): SafeHtml {
  return sanitizer.bypassSecurityTrustHtml(sanitizeLearningHtml(raw));
}

export function isPdfResource(resource: TrainingResourceDto): boolean {
  const t = String(resource.type);
  return t === 'Pdf' || t === '0' || (resource.contentType?.includes('pdf') ?? false);
}

export function isVideoResource(resource: TrainingResourceDto): boolean {
  const t = String(resource.type);
  return t === 'Video' || t === '1';
}

export function isTextResource(resource: TrainingResourceDto): boolean {
  const t = String(resource.type);
  return t === 'Text' || t === '3';
}

export function isImageResource(resource: TrainingResourceDto): boolean {
  const t = String(resource.type);
  return (
    t === 'Image' ||
    t === '4' ||
    (resource.contentType?.startsWith('image/') ?? false)
  );
}

export function isLinkResource(resource: TrainingResourceDto): boolean {
  const t = String(resource.type);
  return t === 'Link' || t === '2';
}

export function isExternalVideoUrl(url: string): boolean {
  return /youtube\.com|youtu\.be|vimeo\.com/i.test(url || '');
}

export function toEmbedUrl(url: string): string {
  let embed = url;
  if (/youtube\.com\/watch\?v=/.test(url)) {
    embed = url.replace('watch?v=', 'embed/');
  } else if (/youtu\.be\//.test(url)) {
    embed = url.replace('youtu.be/', 'www.youtube.com/embed/');
  } else if (/vimeo\.com\/(\d+)/.test(url)) {
    embed = url.replace(/vimeo\.com\/(\d+)/, 'player.vimeo.com/video/$1');
  }
  return embed;
}

export function trustResourceEmbed(
  sanitizer: DomSanitizer,
  resource: TrainingResourceDto,
): SafeResourceUrl | null {
  const url = resource.url || resource.downloadPath;
  if (!url) return null;
  return sanitizer.bypassSecurityTrustResourceUrl(toEmbedUrl(url));
}

/** Compress image file to JPEG data-URL (max edge / size). */
export async function compressImageToDataUrl(
  file: File,
  maxEdge = 1200,
  maxBytes = 800_000,
  quality = 0.82,
): Promise<string> {
  const bitmap = await createImageBitmap(file);
  const scale = Math.min(1, maxEdge / Math.max(bitmap.width, bitmap.height));
  const w = Math.max(1, Math.round(bitmap.width * scale));
  const h = Math.max(1, Math.round(bitmap.height * scale));
  const canvas = document.createElement('canvas');
  canvas.width = w;
  canvas.height = h;
  const ctx = canvas.getContext('2d');
  if (!ctx) throw new Error('Canvas indisponible');
  ctx.drawImage(bitmap, 0, 0, w, h);
  bitmap.close();

  let q = quality;
  let dataUrl = canvas.toDataURL('image/jpeg', q);
  while (dataUrl.length > maxBytes * 1.37 && q > 0.45) {
    q -= 0.1;
    dataUrl = canvas.toDataURL('image/jpeg', q);
  }
  if (dataUrl.length > maxBytes * 1.37) {
    throw new Error('Image trop lourde après compression (max ~800 Ko).');
  }
  return dataUrl;
}
