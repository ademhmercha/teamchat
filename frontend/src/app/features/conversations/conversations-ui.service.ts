import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ConversationsUiService {
  readonly navOpen = signal(false);

  openNav(): void {
    this.navOpen.set(true);
  }

  closeNav(): void {
    this.navOpen.set(false);
  }

  toggleNav(): void {
    this.navOpen.update((open) => !open);
  }
}
