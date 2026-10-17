import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { IconComponent } from '../../shared/icon/icon';
import { ButtonComponent } from '../../shared/ui/button';

interface LandingFeature {
  icon: string;
  title: string;
  text: string;
}

interface LandingStep {
  icon: string;
  title: string;
  text: string;
}

interface LandingHighlight {
  icon: string;
  label: string;
  text: string;
}

@Component({
  selector: 'app-landing',
  imports: [IconComponent, ButtonComponent],
  templateUrl: './landing.html',
  styleUrl: './landing.scss',
})
export class LandingComponent {
  private readonly router = inject(Router);

  readonly year = new Date().getFullYear();

  readonly highlights: LandingHighlight[] = [
    { icon: 'bolt', label: 'Temps réel', text: 'Messages instantanés' },
    { icon: 'video', label: 'Audio & vidéo', text: 'Appels intégrés' },
    { icon: 'smartphone', label: 'Web & mobile', text: 'Installable (PWA)' },
    { icon: 'shield', label: 'Isolé par société', text: 'Données cloisonnées' },
  ];

  readonly features: LandingFeature[] = [
    {
      icon: 'conversations',
      title: 'Messages & groupes',
      text: 'Conversations privées et groupes, indicateurs de saisie et présence en temps réel.',
    },
    {
      icon: 'video',
      title: 'Appels audio, vidéo & écran',
      text: 'Lancez un appel depuis une conversation, avec partage d’écran en un clic.',
    },
    {
      icon: 'paperclip',
      title: 'Fichiers & messages vocaux',
      text: 'Partagez documents, images et notes vocales en toute simplicité.',
    },
    {
      icon: 'users',
      title: 'Annuaire & présence',
      text: 'Retrouvez vos collègues et voyez instantanément qui est disponible.',
    },
    {
      icon: 'bell',
      title: 'Notifications & recherche',
      text: 'Ne manquez plus rien et retrouvez rapidement une conversation.',
    },
    {
      icon: 'dashboard',
      title: 'Console d’administration',
      text: 'Gérez comptes, rôles et groupes depuis un tableau de bord centralisé.',
    },
  ];

  readonly steps: LandingStep[] = [
    {
      icon: 'plus',
      title: 'Créez votre société',
      text: 'L’Owner ouvre un espace de travail dédié à votre entreprise en quelques secondes.',
    },
    {
      icon: 'users',
      title: 'Invitez votre équipe',
      text: 'L’administrateur crée les comptes et attribue les rôles adaptés à chacun.',
    },
    {
      icon: 'conversations',
      title: 'Travaillez ensemble',
      text: 'Discutez, partagez des fichiers et passez des appels, où que vous soyez.',
    },
  ];

  go(path: string): void {
    void this.router.navigateByUrl(path);
  }

  scrollTo(sectionId: string): void {
    document.getElementById(sectionId)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
}
