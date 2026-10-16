import { Component, ElementRef, OnDestroy, effect, inject, signal, viewChild } from '@angular/core';
import { CallService } from '../../core/calls/call.service';
import { AvatarComponent } from '../../shared/ui/avatar';
import { IconComponent } from '../../shared/icon/icon';

@Component({
  selector: 'app-call-overlay',
  imports: [IconComponent, AvatarComponent],
  templateUrl: './call-overlay.html',
  styleUrl: './call-overlay.scss',
})
export class CallOverlayComponent implements OnDestroy {
  readonly call = inject(CallService);

  readonly fullscreen = signal(false);

  private readonly localVideo = viewChild<ElementRef<HTMLVideoElement>>('localVideo');
  private readonly remoteVideo = viewChild<ElementRef<HTMLVideoElement>>('remoteVideo');
  private readonly callRoot = viewChild<ElementRef<HTMLElement>>('callRoot');

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
          void element.play().catch(() => undefined);
        }
      }
    });

    document.addEventListener('fullscreenchange', this.onFullscreenChange);

    effect(() => {
      if (this.call.minimized() && document.fullscreenElement) {
        void document.exitFullscreen().catch(() => undefined);
      }
    });
  }

  ngOnDestroy(): void {
    document.removeEventListener('fullscreenchange', this.onFullscreenChange);
  }

  private readonly onFullscreenChange = (): void => {
    this.fullscreen.set(!!document.fullscreenElement);
  };

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

    // iOS Safari : seul l'element <video> supporte le plein ecran natif.
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
