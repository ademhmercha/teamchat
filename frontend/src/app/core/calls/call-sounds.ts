import { Injectable } from '@angular/core';

type Tone = { readonly freqs: readonly number[]; readonly on: number; readonly off: number };

const RINGTONE: Tone = { freqs: [440, 480], on: 1000, off: 2000 };
const RINGBACK: Tone = { freqs: [425], on: 1000, off: 3000 };

@Injectable({ providedIn: 'root' })
export class CallSounds {
  private context: AudioContext | null = null;
  private timer?: number;
  private gain?: GainNode;
  private oscillators: OscillatorNode[] = [];
  private originalTitle: string | null = null;
  private titleTimer?: number;

  ringtone(): void {
    this.play(RINGTONE);
    if (typeof navigator.vibrate === 'function') {
      navigator.vibrate([400, 200, 400, 1500, 400, 200, 400, 1500, 400, 200, 400]);
    }
  }

  ringback(): void {
    this.play(RINGBACK);
  }

  stop(): void {
    window.clearTimeout(this.timer);
    this.timer = undefined;
    this.silence();
    if (typeof navigator.vibrate === 'function') {
      navigator.vibrate(0);
    }
  }

  blinkTitle(text: string): void {
    if (this.titleTimer !== undefined) {
      return;
    }
    this.originalTitle = document.title;
    let on = true;
    this.titleTimer = window.setInterval(() => {
      document.title = on ? text : (this.originalTitle ?? '');
      on = !on;
    }, 900);
  }

  stopBlinkTitle(): void {
    if (this.titleTimer !== undefined) {
      window.clearInterval(this.titleTimer);
      this.titleTimer = undefined;
    }
    if (this.originalTitle !== null) {
      document.title = this.originalTitle;
      this.originalTitle = null;
    }
  }

  private play(tone: Tone): void {
    this.stop();
    const AudioCtor = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (!AudioCtor) {
      return;
    }

    try {
      this.context ??= new AudioCtor();
      void this.context.resume().catch(() => undefined);
    } catch {
      return;
    }

    const burst = (): void => {
      this.beep(tone);
      this.timer = window.setTimeout(burst, tone.on + tone.off);
    };
    burst();
  }

  private beep(tone: Tone): void {
    const context = this.context;
    if (!context) {
      return;
    }

    this.silence();
    const gain = context.createGain();
    gain.gain.value = 0.0001;
    gain.connect(context.destination);
    const now = context.currentTime;
    gain.gain.exponentialRampToValueAtTime(0.18, now + 0.05);
    gain.gain.setValueAtTime(0.18, now + tone.on / 1000 - 0.06);
    gain.gain.exponentialRampToValueAtTime(0.0001, now + tone.on / 1000);

    this.gain = gain;
    this.oscillators = tone.freqs.map((frequency) => {
      const oscillator = context.createOscillator();
      oscillator.frequency.value = frequency;
      oscillator.connect(gain);
      oscillator.start(now);
      oscillator.stop(now + tone.on / 1000);
      return oscillator;
    });
  }

  private silence(): void {
    this.oscillators.forEach((oscillator) => {
      try {
        oscillator.stop();
      } catch {
        oscillator.disconnect();
      }
    });
    this.oscillators = [];
    this.gain?.disconnect();
    this.gain = undefined;
  }
}
