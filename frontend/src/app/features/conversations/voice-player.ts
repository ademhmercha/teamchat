import { ChangeDetectionStrategy, Component, ElementRef, computed, input, signal, viewChild } from '@angular/core';
import { formatDuration } from '../../core/utils/display';
import { IconComponent } from '../../shared/icon/icon';

/**
 * Lecteur de message vocal : bouton lecture/pause, barre de progression
 * deformable au clavier (input range) et minuteur. Remplace le lecteur audio
 * natif pour un rendu homogene au reste de l'interface.
 */
@Component({
  selector: 'app-voice-player',
  imports: [IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="vp">
      <button
        type="button"
        class="vp__play"
        [attr.aria-label]="playing() ? 'Mettre en pause' : 'Lire le message vocal'"
        (click)="toggle()"
      >
        <app-icon [name]="playing() ? 'pause' : 'play'" [size]="15" />
      </button>

      <div class="vp__body">
        <input
          #seek
          type="range"
          class="vp__range"
          min="0"
          [max]="max() || 1"
          step="0.1"
          [value]="current()"
          aria-label="Position de lecture"
          (input)="onSeekInput($event)"
        />
        <span class="vp__time">{{ label() }}</span>
      </div>

      <audio
        #audio
        [src]="src()"
        preload="metadata"
        (play)="playing.set(true)"
        (pause)="playing.set(false)"
        (loadedmetadata)="onMeta()"
        (timeupdate)="onTime()"
        (ended)="onEnded()"
      ></audio>
    </div>
  `,
  styles: `
    .vp {
      display: flex;
      align-items: center;
      gap: 0.6rem;
      min-width: 220px;
      max-width: 320px;
      padding: 0.4rem 0.7rem 0.4rem 0.45rem;
      background: var(--tc-surface);
      border: 1px solid var(--tc-border);
      border-radius: var(--tc-radius-pill);
    }

    .vp__play {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 32px;
      height: 32px;
      flex-shrink: 0;
      border: none;
      border-radius: 50%;
      background: var(--tc-primary);
      color: var(--tc-on-accent);
      cursor: pointer;
      transition: background var(--tc-fast);
    }

    .vp__play:hover {
      background: var(--tc-primary-strong);
    }

    .vp__play:focus-visible {
      outline: 2px solid var(--tc-primary);
      outline-offset: 2px;
    }

    .vp__body {
      display: flex;
      align-items: center;
      gap: 0.55rem;
      flex: 1;
      min-width: 0;
    }

    .vp__range {
      flex: 1;
      min-width: 0;
      height: 4px;
      accent-color: var(--tc-primary);
      cursor: pointer;
    }

    .vp__time {
      font-size: 0.72rem;
      font-variant-numeric: tabular-nums;
      color: var(--tc-text-muted);
      flex-shrink: 0;
    }

    audio {
      display: none;
    }
  `,
})
export class VoicePlayerComponent {
  readonly src = input('');
  readonly duration = input<number | null>(null);

  private readonly audio = viewChild<ElementRef<HTMLAudioElement>>('audio');

  readonly playing = signal(false);
  readonly current = signal(0);
  readonly total = signal(0);

  readonly max = computed(() => this.total() || this.duration() || 0);

  readonly label = computed(() => {
    const active = this.playing() || this.current() > 0;
    return active
      ? formatDuration(this.current())
      : formatDuration(this.total() || this.duration() || 0);
  });

  toggle(): void {
    const audio = this.audio()?.nativeElement;
    if (!audio) {
      return;
    }

    if (audio.paused) {
      void audio.play().catch(() => this.playing.set(false));
    } else {
      audio.pause();
    }
  }

  onSeekInput(event: Event): void {
    const audio = this.audio()?.nativeElement;
    if (!audio) {
      return;
    }

    const value = Number((event.target as HTMLInputElement).value);
    if (Number.isFinite(value)) {
      audio.currentTime = value;
      this.current.set(value);
    }
  }

  onMeta(): void {
    const duration = this.audio()?.nativeElement.duration;
    if (duration !== undefined && Number.isFinite(duration) && duration > 0) {
      this.total.set(duration);
    }
  }

  onTime(): void {
    this.current.set(this.audio()?.nativeElement.currentTime ?? 0);
  }

  onEnded(): void {
    this.playing.set(false);
    this.current.set(0);
    const audio = this.audio()?.nativeElement;
    if (audio) {
      audio.currentTime = 0;
    }
  }
}
