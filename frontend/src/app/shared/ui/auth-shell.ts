import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { IconComponent } from '../icon/icon';
import { BrandComponent } from './brand';

@Component({
  selector: 'tc-auth-shell',
  imports: [RouterLink, IconComponent, BrandComponent],
  templateUrl: './auth-shell.html',
  styleUrl: './auth-shell.scss',
})
export class AuthShellComponent {
  readonly points = [
    { icon: 'conversations', title: 'Messages en temps réel', text: 'Conversations, groupes et fichiers au même endroit.' },
    { icon: 'video', title: 'Appels audio et vidéo', text: 'Avec partage d’écran, directement dans le navigateur.' },
    { icon: 'shield', title: 'Espace isolé par société', text: 'Vos données ne sont visibles que par votre équipe.' },
  ];
}
