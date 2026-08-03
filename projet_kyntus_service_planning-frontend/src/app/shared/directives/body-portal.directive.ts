import { Directive, ElementRef, OnDestroy, OnInit, inject } from '@angular/core';

const BODY_CLASS = 'ky-body-modal-open';
let openCount = 0;

/**
 * Moves the host element to document.body so position:fixed is viewport-relative,
 * and locks body scroll while at least one such overlay is open.
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
    if (node.parentElement !== document.body) {
      document.body.appendChild(node);
    }
    openCount += 1;
    this.locked = true;
    document.body.classList.add(BODY_CLASS);
  }

  ngOnDestroy(): void {
    if (!this.locked) return;
    openCount = Math.max(0, openCount - 1);
    if (openCount === 0) {
      document.body.classList.remove(BODY_CLASS);
    }
    this.locked = false;
  }
}
