import { Pipe, PipeTransform } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';

/** Autorise une URL (ex. <c>blob:</c>) dans <c>iframe [src]</c>. */
@Pipe({ name: 'safeUrl', standalone: true })
export class SafeUrlPipe implements PipeTransform {
  constructor(private readonly sanitizer: DomSanitizer) {}

  transform(url: string | null | undefined): SafeResourceUrl | null {
    const u = url?.trim();
    if (!u) return null;
    return this.sanitizer.bypassSecurityTrustResourceUrl(u);
  }
}
