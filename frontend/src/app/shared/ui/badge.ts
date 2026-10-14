import { Component, input } from '@angular/core';

export type BadgeVariant = 'neutral' | 'primary' | 'success' | 'warning' | 'danger';

@Component({
  selector: 'tc-badge',
  host: {
    '[class]': "'tc-badge tc-badge--' + variant()",
  },
  template: `<ng-content />`,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      gap: 0.3rem;
      height: 20px;
      padding: 0 0.5rem;
      border-radius: var(--tc-radius-pill);
      font-size: 0.72rem;
      font-weight: 600;
      line-height: 1;
      white-space: nowrap;
    }

    :host(.tc-badge--neutral) {
      background: var(--tc-surface-2);
      color: var(--tc-text-soft);
    }

    :host(.tc-badge--primary) {
      background: var(--tc-primary-soft);
      color: var(--tc-primary);
    }

    :host(.tc-badge--success) {
      background: var(--tc-success-soft);
      color: var(--tc-success);
    }

    :host(.tc-badge--warning) {
      background: var(--tc-warning-soft);
      color: var(--tc-warning);
    }

    :host(.tc-badge--danger) {
      background: var(--tc-danger-soft);
      color: var(--tc-danger);
    }
  `,
})
export class BadgeComponent {
  readonly variant = input<BadgeVariant>('neutral');
}
