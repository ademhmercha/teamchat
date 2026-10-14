import { Component, input } from '@angular/core';

@Component({
  selector: 'tc-spinner',
  host: {
    role: 'status',
    'aria-label': 'Chargement',
  },
  template: `<span class="tc-spinner" [style.width.px]="size()" [style.height.px]="size()"></span>`,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      justify-content: center;
    }

    .tc-spinner {
      display: inline-block;
      border-radius: 50%;
      border: 2px solid var(--tc-border-strong);
      border-top-color: var(--tc-primary);
      animation: tc-spin 0.7s linear infinite;
    }

    @keyframes tc-spin {
      to {
        transform: rotate(360deg);
      }
    }
  `,
})
export class SpinnerComponent {
  readonly size = input(18);
}
