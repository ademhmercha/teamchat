import { Component, ElementRef, HostListener, effect, input, output, viewChild } from '@angular/core';
import { IconComponent } from '../icon/icon';

@Component({
  selector: 'tc-modal',
  imports: [IconComponent],
  template: `
    @if (open()) {
      <div class="tc-modal__backdrop" (click)="onBackdrop()"></div>
      <div
        class="tc-modal"
        role="dialog"
        aria-modal="true"
        [attr.aria-label]="title() || null"
        tabindex="-1"
        #dialog
      >
        <header class="tc-modal__header">
          <h2 class="tc-modal__title">{{ title() }}</h2>
          <button
            type="button"
            class="tc-modal__close"
            aria-label="Fermer"
            (click)="close()"
          >
            <app-icon name="close" [size]="18" />
          </button>
        </header>
        <div class="tc-modal__body">
          <ng-content />
        </div>
      </div>
    }
  `,
  styles: `
    .tc-modal__backdrop {
      position: fixed;
      inset: 0;
      z-index: 900;
      background: var(--tc-scrim);
      animation: tc-fade-in var(--tc-fast);
    }

    .tc-modal {
      position: fixed;
      z-index: 901;
      top: 50%;
      left: 50%;
      transform: translate(-50%, -50%);
      width: min(520px, calc(100vw - 2rem));
      max-height: min(80dvh, 720px);
      display: flex;
      flex-direction: column;
      background: var(--tc-surface);
      border: 1px solid var(--tc-border);
      border-radius: var(--tc-radius-lg);
      box-shadow: var(--tc-shadow-lg);
      animation: tc-pop var(--tc-normal);
      overflow: hidden;
    }

    .tc-modal__header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
      padding: 1rem 1.25rem;
      border-bottom: 1px solid var(--tc-border);
    }

    .tc-modal__title {
      margin: 0;
      font-size: 1.05rem;
      font-weight: 600;
      color: var(--tc-text);
    }

    .tc-modal__close {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 32px;
      height: 32px;
      border: none;
      border-radius: var(--tc-radius-sm);
      background: transparent;
      color: var(--tc-text-soft);
      cursor: pointer;
      transition: background var(--tc-fast), color var(--tc-fast);
    }

    .tc-modal__close:hover {
      background: var(--tc-surface-2);
      color: var(--tc-text);
    }

    .tc-modal__body {
      padding: 1.25rem;
      overflow-y: auto;
    }
  `,
})
export class ModalComponent {
  readonly open = input(false);
  readonly title = input('');
  readonly closeOnBackdrop = input(true);

  readonly closed = output<void>();

  private readonly dialog = viewChild<ElementRef<HTMLElement>>('dialog');
  private previouslyFocused: HTMLElement | null = null;

  constructor() {
    effect(() => {
      if (this.open()) {
        this.previouslyFocused = document.activeElement as HTMLElement | null;
        queueMicrotask(() => {
          const container = this.dialog()?.nativeElement;
          if (!container) {
            return;
          }
          const target =
            container.querySelector<HTMLElement>('[data-autofocus]') ??
            container.querySelector<HTMLElement>(
              'input, select, textarea, button, [tabindex]:not([tabindex="-1"])',
            );
          (target ?? container).focus();
        });
      }
    });
  }

  onBackdrop(): void {
    if (this.closeOnBackdrop()) {
      this.close();
    }
  }

  close(): void {
    this.closed.emit();
    this.previouslyFocused?.focus();
    this.previouslyFocused = null;
  }

  @HostListener('document:keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    if (!this.open()) {
      return;
    }
    if (event.key === 'Escape') {
      event.preventDefault();
      this.close();
    } else if (event.key === 'Tab') {
      this.trapFocus(event);
    }
  }

  private trapFocus(event: KeyboardEvent): void {
    const container = this.dialog()?.nativeElement;
    if (!container) {
      return;
    }
    const focusable = container.querySelectorAll<HTMLElement>(
      'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])',
    );
    if (focusable.length === 0) {
      return;
    }
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const active = document.activeElement;

    if (event.shiftKey && active === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && active === last) {
      event.preventDefault();
      first.focus();
    }
  }
}
