import { Component, HostListener, input, output } from '@angular/core';
import { IconComponent } from '../icon/icon';

@Component({
  selector: 'tc-lightbox',
  imports: [IconComponent],
  template: `
    @if (open()) {
      <div
        class="tc-lightbox"
        role="dialog"
        aria-modal="true"
        [attr.aria-label]="alt() || 'Aperçu de l’image'"
        (click)="close()"
      >
        <button type="button" class="tc-lightbox__close" aria-label="Fermer l’aperçu" (click)="close()">
          <app-icon name="close" [size]="20" />
        </button>
        <img
          class="tc-lightbox__img"
          [src]="src()"
          [alt]="alt()"
          (click)="$event.stopPropagation()"
        />
      </div>
    }
  `,
  styles: `
    .tc-lightbox {
      position: fixed;
      inset: 0;
      z-index: 1000;
      display: grid;
      place-items: center;
      padding: 2.5rem;
      background: var(--tc-scrim-strong);
      animation: tc-fade-in var(--tc-fast);
      cursor: zoom-out;
    }

    .tc-lightbox__img {
      max-width: min(92vw, 1400px);
      max-height: 88vh;
      width: auto;
      height: auto;
      border-radius: var(--tc-radius-lg);
      box-shadow: var(--tc-shadow-lg);
      cursor: default;
      animation: tc-pop var(--tc-normal);
    }

    .tc-lightbox__close {
      position: absolute;
      top: 1rem;
      right: 1rem;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 40px;
      height: 40px;
      border: none;
      border-radius: 50%;
      background: rgba(255, 255, 255, 0.14);
      color: var(--tc-on-accent);
      cursor: pointer;
      transition: background var(--tc-fast);
    }

    .tc-lightbox__close:hover {
      background: rgba(255, 255, 255, 0.26);
    }

    .tc-lightbox__close:focus-visible {
      outline: 2px solid var(--tc-on-accent);
      outline-offset: 2px;
    }
  `,
})
export class LightboxComponent {
  readonly open = input(false);
  readonly src = input('');
  readonly alt = input('');

  readonly closed = output<void>();

  close(): void {
    this.closed.emit();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open()) {
      this.close();
    }
  }
}
