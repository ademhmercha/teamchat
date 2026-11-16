import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { IconComponent } from '../../shared/icon/icon';
import { AuthShellComponent } from '../../shared/ui/auth-shell';
import { ButtonComponent } from '../../shared/ui/button';

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink, ButtonComponent, AuthShellComponent, IconComponent],
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

  readonly showPassword = signal(false);

  private readonly passwordValue = toSignal(this.form.controls.password.valueChanges, { initialValue: '' });

  readonly strength = computed(() => {
    const value = this.passwordValue();
    const rules = [
      { label: '8 caractères minimum', ok: value.length >= 8 },
      { label: 'Une minuscule', ok: /[a-z]/.test(value) },
      { label: 'Une majuscule', ok: /[A-Z]/.test(value) },
      { label: 'Un chiffre', ok: /[0-9]/.test(value) },
      { label: 'Un caractère spécial', ok: /[^a-zA-Z0-9]/.test(value) },
    ];
    const passed = rules.filter((rule) => rule.ok).length;
    const level = value.length === 0 ? 0 : Math.min(4, Math.max(1, passed - (passed === 5 && value.length < 12 ? 1 : 0)));
    return { rules, level };
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
