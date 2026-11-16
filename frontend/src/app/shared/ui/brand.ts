import { Component, input } from '@angular/core';

let nextBrandId = 0;

@Component({
  selector: 'tc-brand',
  template: `
    <span class="brand" [class.light]="tone() === 'light'">
      <svg
        [attr.width]="size()"
        [attr.height]="size()"
        viewBox="0 0 48 48"
        fill="none"
        aria-hidden="true"
        focusable="false"
      >
        <defs>
          <linearGradient [attr.id]="gradientId" x1="4" y1="2" x2="44" y2="46" gradientUnits="userSpaceOnUse">
            <stop stop-color="#38bdf8" />
            <stop offset="0.5" stop-color="#2563eb" />
            <stop offset="1" stop-color="#4f46e5" />
          </linearGradient>
        </defs>
        <rect width="48" height="48" rx="13" [attr.fill]="'url(#' + gradientId + ')'" />
        <path
          d="M12 13h20a4 4 0 0 1 4 4v10a4 4 0 0 1-4 4H22l-6 5v-5h-4a4 4 0 0 1-4-4V17a4 4 0 0 1 4-4z"
          fill="#fff"
        />
        <path d="M37 20.5l6-3.5v11l-6-3.5z" fill="#fff" fill-opacity="0.9" />
        <circle cx="16" cy="22" r="1.9" fill="#2563eb" />
        <circle cx="22" cy="22" r="1.9" fill="#2563eb" />
        <circle cx="28" cy="22" r="1.9" fill="#2563eb" />
      </svg>
      @if (wordmark()) {
        <span class="word" [style.font-size.px]="size() * 0.58">Team<b>Chat</b></span>
      }
    </span>
  `,
  styles: `
    :host {
      display: inline-flex;
    }

    .brand {
      display: inline-flex;
      align-items: center;
      gap: 0.6em;
      color: var(--tc-text);
      line-height: 1;
    }

    .brand.light {
      color: #fff;
    }

    svg {
      flex-shrink: 0;
      filter: drop-shadow(0 4px 10px rgba(37, 99, 235, 0.35));
    }

    .word {
      font-weight: 700;
      letter-spacing: -0.03em;
    }

    .word b {
      font-weight: 800;
      background: linear-gradient(100deg, #38bdf8, #6366f1);
      -webkit-background-clip: text;
      background-clip: text;
      color: transparent;
    }
  `,
})
export class BrandComponent {
  readonly size = input(36);
  readonly wordmark = input(true);
  readonly tone = input<'dark' | 'light'>('dark');

  protected readonly gradientId = `tc-brand-${nextBrandId++}`;
}
