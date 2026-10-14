import { Component, inject } from '@angular/core';
import { IconComponent } from '../../shared/icon/icon';
import { EmptyStateComponent } from '../../shared/ui/empty-state';
import { TooltipDirective } from '../../shared/ui/tooltip';
import { ConversationsUiService } from './conversations-ui.service';

@Component({
  selector: 'app-conversations-empty',
  imports: [IconComponent, EmptyStateComponent, TooltipDirective],
  template: `
    <div class="empty">
      <button
        type="button"
        class="menu-btn"
        tcTooltip="Ouvrir les conversations"
        (click)="ui.openNav()"
      >
        <app-icon name="menu" [size]="20" />
      </button>
      <tc-empty-state
        icon="conversations"
        title="Vos messages"
        message="Sélectionnez une conversation à gauche ou démarrez un nouvel échange."
      />
    </div>
  `,
  styles: `
    .empty {
      flex: 1;
      display: grid;
      place-items: center;
      padding: 2rem;
      position: relative;
      min-height: 0;
    }

    .menu-btn {
      display: none;
      position: absolute;
      top: 1rem;
      left: 1rem;
      width: 40px;
      height: 40px;
      align-items: center;
      justify-content: center;
      border: 1px solid var(--tc-border-strong);
      border-radius: var(--tc-radius-sm);
      background: var(--tc-surface);
      color: var(--tc-text-soft);
      cursor: pointer;
    }

    @media (max-width: 900px) {
      .menu-btn {
        display: inline-flex;
      }
    }
  `,
})
export class ConversationsEmptyComponent {
  readonly ui = inject(ConversationsUiService);
}
