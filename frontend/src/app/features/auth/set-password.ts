import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { IconComponent } from '../../shared/icon/icon';
import { ButtonComponent } from '../../shared/ui/button';

const PASSWORD_PATTERN = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).+$/;

function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const password = group.get('password')?.value;
  const confirmation = group.get('confirmation')?.value;
  return password && confirmation && password !== confirmation ? { mismatch: true } : null;
}

@Component({
  selector: 'app-set-password',
  imports: [ReactiveFormsModule, RouterLink, IconComponent, ButtonComponent],
  templateUrl: './set-password.html',
  styleUrl: './set-password.scss',
})
export class SetPasswordComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly done = signal(false);
  readonly invalidLink = signal(false);

  readonly form = this.formBuilder.group(
    {
      password: ['', [Validators.required, Validators.minLength(8), Validators.pattern(PASSWORD_PATTERN)]],
      confirmation: ['', [Validators.required]],
    },
    { validators: passwordsMatch },
  );

  private userId = '';
  private token = '';
  private mode: 'reset' | 'invite' = 'reset';

  get isInvite(): boolean {
    return this.mode === 'invite';
  }

  goToLogin(): void {
    void this.router.navigateByUrl('/login');
  }

  ngOnInit(): void {
    this.mode = this.router.url.startsWith('/accept-invite') ? 'invite' : 'reset';
    this.userId = this.route.snapshot.queryParamMap.get('userId') ?? '';
    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';
    this.invalidLink.set(!this.userId || !this.token);
  }

  submit(): void {
    if (this.invalidLink() || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    const { password } = this.form.getRawValue();
    const request$ = this.isInvite
      ? this.auth.acceptInvite(this.userId, this.token, password)
      : this.auth.resetPassword(this.userId, this.token, password);

    request$.subscribe({
      next: () => {
        this.done.set(true);
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(this.readErrorMessage(error));
        this.loading.set(false);
      },
    });
  }

  private readErrorMessage(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return "Impossible de joindre le serveur. Verifiez qu'il est bien demarre.";
    }

    const problem = error.error as
      | { detail?: string; title?: string; errors?: Record<string, string[]> }
      | null;

    if (problem?.errors) {
      const first = Object.values(problem.errors).flat()[0];
      if (first) {
        return first;
      }
    }

    if (error.status === 400 || error.status === 404) {
      return 'Lien invalide ou expire. Demandez un nouveau lien.';
    }

    return problem?.detail ?? problem?.title ?? 'Operation impossible. Reessayez plus tard.';
  }
}
