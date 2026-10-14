import { Component, inject } from '@angular/core';
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
}
