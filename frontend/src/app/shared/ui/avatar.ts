import { HttpClient } from '@angular/common/http';
import { Component, OnDestroy, computed, effect, inject, input, signal } from '@angular/core';
import { environment } from '../../../environments/environment';

export type AvatarSize = 'sm' | 'md' | 'lg' | 'xl';

@Component({
  selector: 'tc-avatar',
  host: {
    '[class]':
      "'tc-avatar tc-avatar--' + size() + (group() ? ' tc-avatar--group' : '') + (online() === true ? ' tc-avatar--online' : '')",
  },
  template: `
    @if (imageSrc()) {
      <img class="tc-avatar__img" [src]="imageSrc()" [alt]="name() || 'Avatar'" loading="lazy" />
    } @else {
      <span class="tc-avatar__initials" aria-hidden="true">{{ initials() }}</span>
    }
    @if (online() !== null) {
      <span class="tc-avatar__dot" [attr.aria-label]="online() ? 'En ligne' : 'Hors ligne'"></span>
    }
  `,
  styles: `
    :host {
      position: relative;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 40px;
      height: 40px;
      border-radius: 50%;
      background: var(--tc-primary-soft);
      color: var(--tc-primary);
      font-weight: 600;
      flex-shrink: 0;
      overflow: visible;
      user-select: none;
    }

    :host(.tc-avatar--sm) {
      width: 32px;
      height: 32px;
      font-size: 0.72rem;
    }

    :host(.tc-avatar--md) {
      font-size: 0.82rem;
    }

    :host(.tc-avatar--lg) {
      width: 48px;
      height: 48px;
      font-size: 0.95rem;
    }

    :host(.tc-avatar--xl) {
      width: 72px;
      height: 72px;
      font-size: 1.35rem;
    }

    :host(.tc-avatar--group) {
      background: var(--tc-group-avatar);
      color: var(--tc-group-avatar-text);
    }

    .tc-avatar__img {
      width: 100%;
      height: 100%;
      border-radius: 50%;
      object-fit: cover;
    }

    .tc-avatar__initials {
      line-height: 1;
    }

    .tc-avatar__dot {
      position: absolute;
      right: -1px;
      bottom: -1px;
      width: 11px;
      height: 11px;
      border-radius: 50%;
      background: var(--tc-text-muted);
      border: 2px solid var(--tc-surface);
    }

    :host(.tc-avatar--online) .tc-avatar__dot {
      background: var(--tc-success);
    }
  `,
})
export class AvatarComponent implements OnDestroy {
  private readonly http = inject(HttpClient);

  readonly name = input('');
  readonly src = input<string | null>(null);
  readonly size = input<AvatarSize>('md');
  readonly group = input(false);
  readonly online = input<boolean | null>(null);

  private readonly _resolved = signal<string | null>(null);
  private objectUrl: string | null = null;
  private requested: string | null = null;

  /**
   * L'endpoint d'avatar de l'API exige un jeton : une balise <img> ne peut pas
   * l'envoyer. On recupere donc l'image via HttpClient (qui ajoute le Bearer
   * grace a l'intercepteur) et on expose un object URL local.
   */
  readonly imageSrc = computed(() => {
    const src = this.src();
    if (!src) {
      return null;
    }

    return this.needsAuth(src) ? this._resolved() : src;
  });

  readonly initials = computed(() => {
    const value = this.name().trim();
    if (!value) {
      return '?';
    }
    return value
      .split(/\s+/)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase() ?? '')
      .join('');
  });

  constructor() {
    effect(() => {
      const src = this.src();

      if (!src || !this.needsAuth(src)) {
        this.release();
        return;
      }

      if (src === this.requested) {
        return;
      }

      this.requested = src;
      this.release();

      this.http.get(src, { responseType: 'blob' }).subscribe({
        next: (blob) => {
          if (this.requested !== src) {
            return;
          }
          this.objectUrl = URL.createObjectURL(blob);
          this._resolved.set(this.objectUrl);
        },
        error: () => {
          if (this.requested === src) {
            this._resolved.set(null);
          }
        },
      });
    });
  }

  ngOnDestroy(): void {
    this.release();
  }

  private needsAuth(src: string): boolean {
    return src.startsWith(environment.apiBaseUrl) || src.includes('/api/');
  }

  private release(): void {
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
      this.objectUrl = null;
    }
    this._resolved.set(null);
  }
}
