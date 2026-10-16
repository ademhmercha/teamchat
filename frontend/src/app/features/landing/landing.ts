import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { IconComponent } from '../../shared/icon/icon';
import { ButtonComponent } from '../../shared/ui/button';

@Component({
  selector: 'app-landing',
  imports: [IconComponent, ButtonComponent],
  templateUrl: './landing.html',
  styleUrl: './landing.scss',
})
export class LandingComponent {
  private readonly router = inject(Router);

  go(path: string): void {
    void this.router.navigateByUrl(path);
  }
}
