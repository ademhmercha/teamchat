import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/auth/auth.service';

declare const google: {
  accounts: {
    id: {
      initialize: (config: { client_id: string; callback: (response: { credential: string }) => void }) => void;
      renderButton: (parent: HTMLElement | null, options: Record<string, unknown>) => void;
    };
  };
};

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class LoginComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly googleEnabled = environment.googleClientId.length > 0;

  readonly form = this.formBuilder.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  ngOnInit(): void {
    if (this.googleEnabled) {
      this.loadGoogleSignIn();
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    const { email, password } = this.form.getRawValue();

    this.auth.login(email, password).subscribe({
      next: () => this.redirectAfterLogin(),
      error: (error: HttpErrorResponse) => {
        this.error.set(this.readErrorMessage(error));
        this.loading.set(false);
      },
    });
  }

  private loadGoogleSignIn(): void {
    const script = document.createElement('script');
    script.src = 'https://accounts.google.com/gsi/client';
    script.async = true;
    script.defer = true;
    script.onload = () => {
      google.accounts.id.initialize({
        client_id: environment.googleClientId,
        callback: (response) => this.onGoogleCredential(response.credential),
      });
      google.accounts.id.renderButton(document.getElementById('google-button'), {
        theme: 'outline',
        size: 'large',
        width: 320,
      });
    };
    document.head.appendChild(script);
  }

  private onGoogleCredential(credential: string): void {
    this.loading.set(true);
    this.error.set(null);

    this.auth.loginWithGoogle(credential).subscribe({
      next: () => this.redirectAfterLogin(),
      error: (error: HttpErrorResponse) => {
        this.error.set(this.readErrorMessage(error));
        this.loading.set(false);
      },
    });
  }

  private redirectAfterLogin(): void {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/';
    void this.router.navigateByUrl(returnUrl);
  }

  private readErrorMessage(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return "Impossible de joindre le serveur. Verifiez qu'il est bien demarre.";
    }

    const problem = error.error as { detail?: string; title?: string } | null;
    return problem?.detail ?? problem?.title ?? 'Connexion impossible.';
  }
}
