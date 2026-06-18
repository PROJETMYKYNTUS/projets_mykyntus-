import {
  AfterViewInit,
  Directive,
  ElementRef,
  EventEmitter,
  HostListener,
  Input,
  Output,
  inject,
  OnChanges,
} from '@angular/core';

/**
 * Lie un <select> natif au modèle : la valeur DOM reste alignée sur le modèle
 * (évite les écarts [value] / signal Angular vs choix utilisateur).
 */
@Directive({
  selector: 'select[kyntusSelectSync]',
  standalone: true,
})
export class KyntusSelectSyncDirective implements OnChanges, AfterViewInit {
  private readonly el = inject(ElementRef<HTMLSelectElement>);

  @Input() kyntusSelectSync = '';
  @Output() kyntusSelectSyncChange = new EventEmitter<string>();

  ngAfterViewInit(): void {
    this.syncToDom();
  }

  ngOnChanges(): void {
    this.syncToDom();
  }

  @HostListener('change')
  onChange(): void {
    this.kyntusSelectSyncChange.emit(this.el.nativeElement.value ?? '');
  }

  private syncToDom(): void {
    const node = this.el.nativeElement;
    const next = this.kyntusSelectSync ?? '';
    if (node.value !== next) {
      node.value = next;
    }
  }
}
