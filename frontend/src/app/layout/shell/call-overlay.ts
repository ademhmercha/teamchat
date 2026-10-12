import { Component, ElementRef, effect, inject, viewChild } from '@angular/core';
import { CallService } from '../../core/calls/call.service';
import { initialsOf } from '../../core/utils/display';

@Component({
  selector: 'app-call-overlay',
  templateUrl: './call-overlay.html',
  styleUrl: './call-overlay.scss',
})
export class CallOverlayComponent {
  readonly call = inject(CallService);

  private readonly localVideo = viewChild<ElementRef<HTMLVideoElement>>('localVideo');
  private readonly remoteVideo = viewChild<ElementRef<HTMLVideoElement>>('remoteVideo');

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
  }

  initials(): string {
    const incoming = this.call.incoming();
    return initialsOf(incoming?.callerDisplayName ?? this.call.peerName());
  }
}
