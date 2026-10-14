import { Component, HostListener, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ConversationsListComponent } from './conversations-list';
import { ConversationsUiService } from './conversations-ui.service';

@Component({
  selector: 'app-conversations-layout',
  imports: [RouterOutlet, ConversationsListComponent],
  templateUrl: './conversations-layout.html',
  styleUrl: './conversations-layout.scss',
})
export class ConversationsLayoutComponent {
  readonly ui = inject(ConversationsUiService);

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.ui.navOpen()) {
      this.ui.closeNav();
    }
  }
}
