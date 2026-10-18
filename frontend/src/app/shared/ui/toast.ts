import { Component, Injectable, inject, signal } from '@angular/core';
import { IconComponent } from '../icon/icon';

export type ToastVariant = 'success' | 'error' | 'info';

interface Toast {
  readonly id: number;
  readonly message: string;
  readonly variant: ToastVariant;
  readonly icon: string;
}

const VARIANT_ICON: Record<ToastVariant, string> = {
  success: 'check',
  error: 'warning',
  info: 'info',
};

/** Le message d'erreur reste affiché plus longtemps : il demande une reaction. */
const VARIANT_DURATION: Record<ToastVariant, number> = {
  success: 4500,
  info: 5000,
  error: 7000,
};

const MAX_VISIBLE = 5;

/**
 * Notifications globales (systeme « toast »), pouvues depuis n'importe
 * quel composant et affichees par ToastHostComponent monte dans app.html
 * (hors router-outlet : le message survit donc aux changements de route).
 *
 * Utilisee pour le feedback transverse ; les erreurs liees a un formulaire
 * ou a un chargement de page restent en message inline, a cote de l'action.
 */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly _items = signal<Toast[]>([]);
  readonly items = this._items.asReadonly();

  private nextId = 0;

  success(message: string): void {
    this.push(message, 'success');
  }

  error(message: string): void {
    this.push(message, 'error');
  }

  info(message: string): void {
    this.push(message, 'info');
  }

  dismiss(id: number): void {
    this._items.update((items) => items.filter((item) => item.id !== id));
  }

  private push(message: string, variant: ToastVariant): void {
    const id = ++this.nextId;
    // La file est bornée : un empilement infini masquerait la base de l'ecran.
    this._items.update((items) => [
      ...items.slice(Math.max(0, items.length - (MAX_VISIBLE - 1))),
      { id, message, variant, icon: VARIANT_ICON[variant] },
    ]);
    window.setTimeout(() => this.dismiss(id), VARIANT_DURATION[variant]);
  }
}

@Component({
  selector: 'tc-toast-host',
  imports: [IconComponent],
  template: `
    <div class="tc-toasts" role="status" aria-live="polite" aria-atomic="false">
      @for (item of service.items(); track item.id) {
        <div class="tc-toast" [attr.data-variant]="item.variant">
          <app-icon [name]="item.icon" [size]="18" />
          <span class="tc-toast__message">{{ item.message }}</span>
          <button
            type="button"
            class="tc-toast__close"
            aria-label="Fermer la notification"
            (click)="service.dismiss(item.id)"
          >
            <app-icon name="close" [size]="15" />
          </button>
        </div>
      }
    </div>
  `,
  styles: `
    :host {
      position: fixed;
      z-index: 1300;
      right: 1rem;
      bottom: 1rem;
      display: flex;
      flex-direction: column;
      width: min(360px, calc(100vw - 2rem));
      pointer-events: none;
    }

    .tc-toasts {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }

    .tc-toast {
      display: flex;
      align-items: flex-start;
      gap: 0.6rem;
      padding: 0.7rem 0.8rem;
      border: 1px solid var(--tc-border);
      border-left-width: 3px;
      border-radius: var(--tc-radius);
      background: var(--tc-surface);
      box-shadow: var(--tc-shadow-lg);
      color: var(--tc-text);
      font-size: 0.85rem;
      line-height: 1.45;
      pointer-events: auto;
      animation: tc-slide-in-right var(--tc-normal);
    }

    .tc-toast[data-variant='success'] {
      background: var(--tc-success-soft);
      border-left-color: var(--tc-success);
      color: var(--tc-success);
    }

    .tc-toast[data-variant='error'] {
      background: var(--tc-danger-soft);
      border-left-color: var(--tc-danger);
      color: var(--tc-danger);
    }

    .tc-toast[data-variant='info'] {
      background: var(--tc-primary-soft);
      border-left-color: var(--tc-accent);
      color: var(--tc-primary-strong);
    }

    .tc-toast__message {
      min-width: 0;
      overflow-wrap: anywhere;
    }

    .tc-toast__close {
      flex-shrink: 0;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 24px;
      height: 24px;
      margin-left: auto;
      border: none;
      border-radius: var(--tc-radius-sm);
      background: transparent;
      color: inherit;
      cursor: pointer;
      opacity: 0.7;
      transition: opacity var(--tc-fast), background var(--tc-fast);
    }

    .tc-toast__close:hover {
      opacity: 1;
      background: var(--tc-glass);
    }

    .tc-toast__close:focus-visible {
      outline: none;
      box-shadow: var(--tc-ring);
    }
  `,
})
export class ToastHostComponent {
  readonly service = inject(ToastService);
}
