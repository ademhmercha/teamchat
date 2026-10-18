import { Component, input, output, viewChild } from '@angular/core';
import { ModalComponent } from './modal';

/**
 * Dialogue de confirmation systeme, construit sur tc-modal
 * (piege de focus + fermeture par Echap deja geres).
 *
 * Le focus initial est place sur « Annuler » : sur une action
 * destructive, la touche Entree ne doit jamais la declencher par accident.
 */
@Component({
  selector: 'tc-confirm',
  imports: [ModalComponent],
  template: `
    <tc-modal
      [open]="open()"
      [title]="title()"
      [closeOnBackdrop]="false"
      (closed)="onClosed()"
      #dialog
    >
      <p class="tc-confirm__message">{{ message() }}</p>
      <div class="tc-confirm__actions">
        <button type="button" class="tc-confirm__cancel" data-autofocus (click)="onClosed()">
          {{ cancelLabel() }}
        </button>
        <button
          type="button"
          class="tc-confirm__confirm"
          [class.is-danger]="danger()"
          (click)="onConfirm()"
        >
          {{ confirmLabel() }}
        </button>
      </div>
    </tc-modal>
  `,
  styles: `
    .tc-confirm__message {
      margin: 0;
      color: var(--tc-text-soft);
      line-height: 1.55;
      white-space: pre-line;
    }

    .tc-confirm__actions {
      display: flex;
      justify-content: flex-end;
      gap: 0.5rem;
      margin-top: 1.25rem;
    }

    button {
      height: 34px;
      padding: 0 0.9rem;
      border: 1px solid transparent;
      border-radius: var(--tc-radius);
      font-family: inherit;
      font-size: 0.875rem;
      font-weight: 600;
      line-height: 1;
      cursor: pointer;
      transition:
        background var(--tc-fast),
        border-color var(--tc-fast),
        color var(--tc-fast);
    }

    button:focus-visible {
      outline: none;
      box-shadow: var(--tc-ring);
    }

    .tc-confirm__cancel {
      background: var(--tc-surface-2);
      border-color: var(--tc-border);
      color: var(--tc-text);
    }

    .tc-confirm__cancel:hover {
      background: var(--tc-surface-3);
    }

    .tc-confirm__confirm {
      background: var(--tc-primary);
      color: var(--tc-on-accent);
    }

    .tc-confirm__confirm:hover {
      background: var(--tc-primary-strong);
    }

    .tc-confirm__confirm.is-danger {
      background: var(--tc-danger);
    }

    .tc-confirm__confirm.is-danger:hover {
      background: var(--tc-danger-strong);
    }
  `,
})
export class ConfirmComponent {
  readonly open = input(false);
  readonly title = input('Confirmer');
  readonly message = input('');
  readonly confirmLabel = input('Confirmer');
  readonly cancelLabel = input('Annuler');
  readonly danger = input(true);

  readonly confirmed = output<void>();
  readonly cancelled = output<void>();

  private readonly dialog = viewChild.required<ModalComponent>('dialog');
  private resolved = false;

  onConfirm(): void {
    this.resolved = true;
    this.confirmed.emit();
    this.dialog().close();
  }

  onClosed(): void {
    if (this.resolved) {
      this.resolved = false;
      return;
    }
    this.cancelled.emit();
  }
}
