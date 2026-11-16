import { Component, ElementRef, OnDestroy, computed, effect, inject, signal, viewChild } from '@angular/core';
import { CallQuality, CallService } from '../../core/calls/call.service';
import { AvatarComponent } from '../../shared/ui/avatar';
import { IconComponent } from '../../shared/icon/icon';

type Corner = 'tl' | 'tr' | 'bl' | 'br';

const CONTROLS_IDLE_MS = 4000;
const DRAG_THRESHOLD_PX = 4;

@Component({
  selector: 'app-call-overlay',
  imports: [IconComponent, AvatarComponent],
  templateUrl: './call-overlay.html',
  styleUrl: './call-overlay.scss',
})
export class CallOverlayComponent implements OnDestroy {
  readonly call = inject(CallService);

  readonly fullscreen = signal(false);
  readonly menuOpen = signal(false);
  readonly audioBlocked = signal(false);
  readonly corner = signal<Corner>('br');
  readonly dragging = signal(false);
  private readonly dragOffset = signal<{ x: number; y: number } | null>(null);
  private readonly activeUntil = signal(Date.now() + CONTROLS_IDLE_MS);
  private readonly now = signal(Date.now());

  readonly supportsSpeakerChoice =
    typeof HTMLMediaElement !== 'undefined' && 'setSinkId' in HTMLMediaElement.prototype;

  readonly isMinimized = computed(() => this.call.minimized() && this.call.phase() === 'active');
  readonly dragTransform = computed(() => {
    const offset = this.dragOffset();
    return offset ? `translate(${offset.x}px, ${offset.y}px)` : null;
  });
  readonly controlsVisible = computed(() => {
    if (!this.call.showVideo() || this.menuOpen() || this.isMinimized() || this.call.phase() !== 'active') {
      return true;
    }
    return this.now() < this.activeUntil();
  });

  private readonly localVideo = viewChild<ElementRef<HTMLVideoElement>>('localVideo');
  private readonly remoteVideo = viewChild<ElementRef<HTMLVideoElement>>('remoteVideo');
  private readonly screenVideo = viewChild<ElementRef<HTMLVideoElement>>('screenVideo');
  private readonly callRoot = viewChild<ElementRef<HTMLElement>>('callRoot');
  private readonly videoArea = viewChild<ElementRef<HTMLElement>>('videoArea');
  private readonly acceptButton = viewChild<ElementRef<HTMLButtonElement>>('acceptButton');

  private idleTimer?: number;
  private dragStart: { x: number; y: number; moved: boolean } | null = null;

  constructor() {
    effect(() => {
      const stream = this.call.localStreamSignal();
      const element = this.localVideo()?.nativeElement;
      if (element && element.srcObject !== stream) {
        element.srcObject = stream;
      }
    });

    effect(() => {
      const stream = this.call.remoteStream();
      const element = this.remoteVideo()?.nativeElement;
      if (element && element.srcObject !== stream) {
        element.srcObject = stream;
        if (stream) {
          this.tryPlay(element);
        }
      }
    });

    effect(() => {
      const stream = this.call.remoteScreenStream();
      const element = this.screenVideo()?.nativeElement;
      if (element && element.srcObject !== stream) {
        element.srcObject = stream;
        if (stream) {
          void element.play().catch(() => undefined);
        }
      }
    });

    effect(() => {
      const sinkId = this.call.selectedSpeakerId();
      const element = this.remoteVideo()?.nativeElement as (HTMLVideoElement & { setSinkId?: (id: string) => Promise<void> }) | undefined;
      if (sinkId && element?.setSinkId) {
        void element.setSinkId(sinkId).catch(() => undefined);
      }
    });

    effect(() => {
      this.acceptButton()?.nativeElement.focus();
    });

    effect(() => {
      if (this.call.phase() === 'idle' || this.call.phase() === 'ended') {
        this.menuOpen.set(false);
        this.audioBlocked.set(false);
        this.corner.set('br');
      }
    });

    document.addEventListener('fullscreenchange', this.onFullscreenChange);
    document.addEventListener('keydown', this.onShortcut);
    this.idleTimer = window.setInterval(() => this.now.set(Date.now()), 1000);

    effect(() => {
      if (this.call.minimized() && document.fullscreenElement) {
        void document.exitFullscreen().catch(() => undefined);
      }
    });
  }

  ngOnDestroy(): void {
    document.removeEventListener('fullscreenchange', this.onFullscreenChange);
    document.removeEventListener('keydown', this.onShortcut);
    window.clearInterval(this.idleTimer);
  }

  qualityLabel(quality: CallQuality): string {
    switch (quality) {
      case 'good':
        return 'Connexion bonne';
      case 'fair':
        return 'Connexion moyenne';
      default:
        return 'Connexion faible';
    }
  }

  valueOf(event: Event): string {
    return (event.target as HTMLSelectElement).value;
  }

  wakeControls(): void {
    const now = Date.now();
    this.now.set(now);
    this.activeUntil.set(now + CONTROLS_IDLE_MS);
  }

  toggleMenu(): void {
    this.menuOpen.update((open) => !open);
  }

  enableAudio(): void {
    const element = this.remoteVideo()?.nativeElement;
    if (element) {
      this.tryPlay(element);
    }
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      if (this.menuOpen()) {
        this.menuOpen.set(false);
      } else if (this.call.enlarged()) {
        this.call.toggleEnlarge();
      }
      return;
    }

    if (event.key !== 'Tab' || this.isMinimized()) {
      return;
    }

    const root = this.callRoot()?.nativeElement;
    if (!root) {
      return;
    }

    const focusable = Array.from(root.querySelectorAll<HTMLElement>('button, select, [href], [tabindex]:not([tabindex="-1"])')).filter(
      (element) => !element.hasAttribute('disabled') && element.offsetParent !== null,
    );
    if (focusable.length === 0) {
      return;
    }

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }

  private readonly onShortcut = (event: KeyboardEvent): void => {
    if (this.call.phase() !== 'active' || !event.ctrlKey || !event.shiftKey) {
      return;
    }

    const key = event.key.toLowerCase();
    if (key === 'm') {
      event.preventDefault();
      this.call.toggleMute();
    } else if (key === 'o') {
      event.preventDefault();
      void this.call.toggleCamera();
    }
  };

  private readonly onFullscreenChange = (): void => {
    this.fullscreen.set(!!document.fullscreenElement);
  };

  private tryPlay(element: HTMLVideoElement): void {
    element.play().then(
      () => this.audioBlocked.set(false),
      () => this.audioBlocked.set(true),
    );
  }

  startDrag(event: PointerEvent): void {
    const target = event.currentTarget as HTMLElement;
    target.setPointerCapture(event.pointerId);
    this.dragStart = { x: event.clientX, y: event.clientY, moved: false };
  }

  moveDrag(event: PointerEvent): void {
    if (!this.dragStart) {
      return;
    }

    const dx = event.clientX - this.dragStart.x;
    const dy = event.clientY - this.dragStart.y;
    if (!this.dragStart.moved && Math.hypot(dx, dy) < DRAG_THRESHOLD_PX) {
      return;
    }

    this.dragStart.moved = true;
    this.dragging.set(true);
    this.dragOffset.set({ x: dx, y: dy });
  }

  endDrag(event: PointerEvent): void {
    const start = this.dragStart;
    this.dragStart = null;
    const area = this.videoArea()?.nativeElement;
    if (!start?.moved || !area) {
      this.dragging.set(false);
      this.dragOffset.set(null);
      return;
    }

    const bounds = area.getBoundingClientRect();
    const horizontal = event.clientX < bounds.left + bounds.width / 2 ? 'l' : 'r';
    const vertical = event.clientY < bounds.top + bounds.height / 2 ? 't' : 'b';
    this.corner.set(`${vertical}${horizontal}` as Corner);
    this.dragging.set(false);
    this.dragOffset.set(null);
  }

  toggleFullscreen(): void {
    if (document.fullscreenElement) {
      void document.exitFullscreen().catch(() => undefined);
      return;
    }

    const root = this.callRoot()?.nativeElement;
    if (root?.requestFullscreen) {
      void root.requestFullscreen().catch(() => undefined);
      return;
    }

    this.enterVideoFullscreen(this.remoteVideo()?.nativeElement ?? this.localVideo()?.nativeElement);
  }

  toggleLocalFullscreen(): void {
    if (document.fullscreenElement) {
      void document.exitFullscreen().catch(() => undefined);
      return;
    }

    const local = this.localVideo()?.nativeElement;
    if (local?.requestFullscreen) {
      void local.requestFullscreen().catch(() => undefined);
      return;
    }

    this.enterVideoFullscreen(local);
  }

  private enterVideoFullscreen(video?: HTMLVideoElement): void {
    if (!video) {
      return;
    }

    const iosVideo = video as HTMLVideoElement & { webkitEnterFullscreen?: () => void };
    iosVideo.webkitEnterFullscreen?.();
  }
}
