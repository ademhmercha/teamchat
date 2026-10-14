import { Component, computed, input, output } from '@angular/core';
import { IconComponent } from '../icon/icon';
import { SpinnerComponent } from './spinner';

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';
export type ButtonSize = 'sm' | 'md';

@Component({
  selector: 'tc-button',
  imports: [IconComponent, SpinnerComponent],
  host: {
    '[class]': 'hostClass()',
  },
  template: `
    <button
      [attr.type]="type()"
      [disabled]="disabled() || loading()"
      [attr.aria-busy]="loading() ? 'true' : null"
      [attr.aria-label]="ariaLabel() || null"
      (click)="clicked.emit($event)"
    >
      @if (loading()) {
        <tc-spinner [size]="size() === 'sm' ? 14 : 16" />
      } @else if (icon()) {
        <app-icon [name]="icon()!" [size]="size() === 'sm' ? 16 : 18" />
      }
      <span class="tc-btn__label"><ng-content /></span>
    </button>
  `,
  styles: `
    :host {
      display: inline-flex;
      max-width: 100%;
    }

    :host(.tc-btn--block) {
      display: flex;
      width: 100%;
    }

    button {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 0.45rem;
      width: 100%;
      min-width: 0;
      border: 1px solid transparent;
      border-radius: var(--tc-radius);
      font-weight: 600;
      line-height: 1;
      cursor: pointer;
      transition:
        background var(--tc-fast),
        border-color var(--tc-fast),
        color var(--tc-fast);
    }

    :host(.tc-btn--md) button {
      padding: 0 0.9rem;
      height: 38px;
      font-size: 0.875rem;
    }

    :host(.tc-btn--sm) button {
      padding: 0 0.65rem;
      height: 30px;
      font-size: 0.8rem;
    }

    :host(.tc-btn--primary) button {
      background: var(--tc-primary);
      color: var(--tc-on-accent);
    }

    :host(.tc-btn--primary) button:hover:not(:disabled) {
      background: var(--tc-primary-strong);
    }

    :host(.tc-btn--secondary) button {
      background: var(--tc-surface);
      border-color: var(--tc-border-strong);
      color: var(--tc-text);
    }

    :host(.tc-btn--secondary) button:hover:not(:disabled) {
      background: var(--tc-surface-2);
    }

    :host(.tc-btn--ghost) button {
      background: transparent;
      color: var(--tc-text-soft);
    }

    :host(.tc-btn--ghost) button:hover:not(:disabled) {
      background: var(--tc-surface-2);
      color: var(--tc-text);
    }

    :host(.tc-btn--danger) button {
      background: var(--tc-danger);
      color: var(--tc-on-accent);
    }

    :host(.tc-btn--danger) button:hover:not(:disabled) {
      background: var(--tc-danger-strong);
    }

    button:disabled {
      opacity: 0.55;
      cursor: not-allowed;
    }
  `,
})
export class ButtonComponent {
  readonly variant = input<ButtonVariant>('secondary');
  readonly size = input<ButtonSize>('md');
  readonly icon = input<string | null>(null);
  readonly type = input<'button' | 'submit'>('button');
  readonly loading = input(false);
  readonly disabled = input(false);
  readonly block = input(false);
  readonly ariaLabel = input('');

  readonly clicked = output<MouseEvent>();

  readonly hostClass = computed(
    () => `tc-btn--${this.variant()} tc-btn--${this.size()}${this.block() ? ' tc-btn--block' : ''}`,
  );
}
