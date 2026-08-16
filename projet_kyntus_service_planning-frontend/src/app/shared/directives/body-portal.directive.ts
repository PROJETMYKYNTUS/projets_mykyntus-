import { Directive, ElementRef, OnDestroy, OnInit, inject } from '@angular/core';

const BODY_CLASS = 'ky-body-modal-open';
const OVERLAY_CLASS = 'ky-viewport-overlay';
let openCount = 0;

/**
 * Moves the host element to document.body so position:fixed is viewport-relative,
 * adds ky-viewport-overlay (global CSS), and locks body scroll while open.
 */
@Directive({
  selector: '[appBodyPortal]',
  standalone: true,
})
export class BodyPortalDirective implements OnInit, OnDestroy {
  private readonly el = inject(ElementRef<HTMLElement>);
  private locked = false;

  ngOnInit(): void {
    const node = this.el.nativeElement;
    node.classList.add(OVERLAY_CLASS);
    if (node.parentElement !== document.body) {
      document.body.appendChild(node);
    }
    openCount += 1;
    this.locked = true;
    document.body.classList.add(BODY_CLASS);
  }

  ngOnDestroy(): void {
    if (!this.locked) return;
    const node = this.el.nativeElement;
    node.classList.remove(OVERLAY_CLASS);
    if (node.parentElement === document.body) {
      try {
        document.body.removeChild(node);
      } catch {
        // Angular may already have detached the node.
      }
    }
    openCount = Math.max(0, openCount - 1);
    if (openCount === 0) {
      document.body.classList.remove(BODY_CLASS);
    }
    this.locked = false;
  }
}
