import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { IconComponent } from '../../shared/icon/icon';
import { ButtonComponent } from '../../shared/ui/button';

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink, IconComponent, ButtonComponent],
  templateUrl: './register.html',
  styleUrl: './register.scss',
})
export class RegisterComponent {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.formBuilder.group({
    companyName: ['', [Validators.required, Validators.maxLength(100)]],
    displayName: ['', [Validators.required, Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email]],
    password: [
      '',
      [
        Validators.required,
        Validators.minLength(8),
        Validators.pattern(/[a-z]/),
        Validators.pattern(/[A-Z]/),
        Validators.pattern(/[0-9]/),
        Validators.pattern(/[^a-zA-Z0-9]/),
      ],
    ],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    const { companyName, displayName, email, password } = this.form.getRawValue();

    this.auth.registerCompany(companyName.trim(), displayName.trim(), email.trim(), password).subscribe({
      next: () => void this.router.navigateByUrl('/app'),
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

    const problem = error.error as {
      detail?: string;
      title?: string;
      errors?: Record<string, string[]>;
    } | null;

    const firstValidationError = problem?.errors
      ? Object.values(problem.errors).flat().find((message) => !!message)
      : undefined;

    return firstValidationError ?? problem?.detail ?? problem?.title ?? "Creation de la societe impossible.";
  }
}
