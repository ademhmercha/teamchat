import { Component, input } from '@angular/core';
import { IconComponent } from '../icon/icon';

@Component({
  selector: 'tc-empty-state',
  imports: [IconComponent],
  template: `
    <div class="tc-empty">
      @if (icon()) {
        <span class="tc-empty__icon"><app-icon [name]="icon()!" [size]="26" /></span>
      }
      <h3 class="tc-empty__title">{{ title() }}</h3>
      @if (message()) {
        <p class="tc-empty__message">{{ message() }}</p>
      }
      <div class="tc-empty__actions"><ng-content /></div>
    </div>
  `,
  styles: `
    :host {
      display: flex;
      flex: 1;
      align-items: center;
      justify-content: center;
      padding: 2rem;
      min-height: 0;
    }

    .tc-empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      text-align: center;
      gap: 0.6rem;
      max-width: 360px;
    }

    .tc-empty__icon {
      display: grid;
      place-items: center;
      width: 56px;
      height: 56px;
      border-radius: 50%;
      background: var(--tc-surface-2);
      color: var(--tc-text-muted);
      margin-bottom: 0.25rem;
    }

    .tc-empty__title {
      margin: 0;
      font-size: 1.05rem;
      font-weight: 600;
      color: var(--tc-text);
    }

    .tc-empty__message {
      margin: 0;
      font-size: 0.875rem;
      color: var(--tc-text-soft);
    }

    .tc-empty__actions:not(:empty) {
      margin-top: 0.75rem;
      display: flex;
      gap: 0.5rem;
    }
  `,
})
export class EmptyStateComponent {
  readonly icon = input<string | null>(null);
  readonly title = input.required<string>();
  readonly message = input<string | null>(null);
}
