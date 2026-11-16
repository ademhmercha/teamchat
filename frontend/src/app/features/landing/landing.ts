import { AfterViewInit, Component, ElementRef, HostListener, OnDestroy, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { IconComponent } from '../../shared/icon/icon';
import { BrandComponent } from '../../shared/ui/brand';

interface LandingFeature {
  icon: string;
  title: string;
  text: string;
  size: 'wide' | 'tall' | 'std';
  visual?: 'chat' | 'call' | 'voice';
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
  imports: [IconComponent, BrandComponent],
  templateUrl: './landing.html',
  styleUrl: './landing.scss',
})
export class LandingComponent implements AfterViewInit, OnDestroy {
  private readonly router = inject(Router);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly year = new Date().getFullYear();
  readonly menuOpen = signal(false);
  readonly scrolled = signal(false);
  readonly revealReady = signal(false);

  readonly waveform = [30, 55, 40, 75, 50, 90, 60, 35, 70, 45, 85, 55, 30, 65, 40, 25];

  private observer?: IntersectionObserver;

  readonly highlights: LandingHighlight[] = [
    { icon: 'bolt', label: 'Temps réel', text: 'Messages instantanés' },
    { icon: 'video', label: 'Audio et vidéo', text: 'Appels et partage d’écran' },
    { icon: 'smartphone', label: 'Web et mobile', text: 'Installable (PWA)' },
    { icon: 'shield', label: 'Isolé par société', text: 'Données cloisonnées' },
  ];

  readonly features: LandingFeature[] = [
    {
      icon: 'conversations',
      title: 'Messages et groupes',
      text: 'Conversations privées et groupes, « en train d’écrire », messages non lus et présence en temps réel.',
      size: 'wide',
      visual: 'chat',
    },
    {
      icon: 'video',
      title: 'Appels, vidéo et écran',
      text: 'Lancez un appel depuis une conversation. Caméra et partage d’écran en même temps.',
      size: 'tall',
      visual: 'call',
    },
    {
      icon: 'paperclip',
      title: 'Fichiers et vocaux',
      text: 'Images, documents et messages vocaux enregistrés dans le navigateur.',
      size: 'std',
      visual: 'voice',
    },
    {
      icon: 'users',
      title: 'Annuaire',
      text: 'Retrouvez vos collègues et voyez qui est disponible.',
      size: 'std',
    },
    {
      icon: 'bell',
      title: 'Notifications et recherche',
      text: 'Ne manquez rien et retrouvez vite une conversation.',
      size: 'std',
    },
    {
      icon: 'dashboard',
      title: 'Console d’administration',
      text: 'Comptes, rôles, groupes et statistiques depuis un tableau de bord centralisé.',
      size: 'wide',
    },
  ];

  readonly steps: LandingStep[] = [
    {
      icon: 'plus',
      title: 'Créez votre société',
      text: 'Vous ouvrez un espace dédié à votre entreprise en quelques secondes et devenez Owner.',
    },
    {
      icon: 'users',
      title: 'Invitez votre équipe',
      text: 'Créez les comptes, attribuez les rôles : chacun reçoit un lien pour activer son accès.',
    },
    {
      icon: 'conversations',
      title: 'Travaillez ensemble',
      text: 'Discutez, partagez des fichiers et passez des appels, où que vous soyez.',
    },
  ];

  ngAfterViewInit(): void {
    if (typeof IntersectionObserver === 'undefined') {
      return;
    }

    this.observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (entry.isIntersecting) {
            entry.target.classList.add('in');
            this.observer?.unobserve(entry.target);
          }
        }
      },
      { threshold: 0.12, rootMargin: '0px 0px -40px 0px' },
    );

    this.host.nativeElement
      .querySelectorAll('[data-reveal]')
      .forEach((element) => this.observer?.observe(element));
    this.revealReady.set(true);
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
  }

  go(path: string): void {
    void this.router.navigateByUrl(path);
  }

  toggleMenu(): void {
    this.menuOpen.update((open) => !open);
  }

  scrollTo(sectionId: string): void {
    this.menuOpen.set(false);
    document.getElementById(sectionId)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  spotlight(event: PointerEvent): void {
    const card = (event.target as HTMLElement).closest<HTMLElement>('.card');
    if (!card) {
      return;
    }

    const bounds = card.getBoundingClientRect();
    card.style.setProperty('--mx', `${event.clientX - bounds.left}px`);
    card.style.setProperty('--my', `${event.clientY - bounds.top}px`);
  }

  @HostListener('window:scroll')
  onScroll(): void {
    this.scrolled.set(window.scrollY > 12);
  }

  @HostListener('window:keydown.escape')
  onEscape(): void {
    this.menuOpen.set(false);
  }
}
