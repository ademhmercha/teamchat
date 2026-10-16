import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { Router } from '@angular/router';
import { CompanyService } from '../../core/api/company.service';
import { AuthService } from '../../core/auth/auth.service';
import { AvatarComponent } from '../../shared/ui/avatar';
import { ButtonComponent } from '../../shared/ui/button';

const PASSWORD_PATTERN = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).+$/;

function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const password = group.get('newPassword')?.value;
  const confirmation = group.get('confirmation')?.value;
  return password && confirmation && password !== confirmation ? { mismatch: true } : null;
}

@Component({
  selector: 'app-account',
  imports: [ReactiveFormsModule, AvatarComponent, ButtonComponent],
  templateUrl: './account.html',
  styleUrl: './account.scss',
})
export class AccountComponent {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly auth = inject(AuthService);
  private readonly companyService = inject(CompanyService);
  private readonly router = inject(Router);

  readonly user = this.auth.user;
  readonly isOwner = this.auth.isOwner;

  readonly companySaving = signal(false);
  readonly companyMessage = signal<string | null>(null);
  readonly companyError = signal<string | null>(null);

  readonly profileSaving = signal(false);
  readonly profileMessage = signal<string | null>(null);
  readonly profileError = signal<string | null>(null);

  readonly passwordSaving = signal(false);
  readonly passwordMessage = signal<string | null>(null);
  readonly passwordError = signal<string | null>(null);

  readonly avatarUploading = signal(false);
  readonly avatarError = signal<string | null>(null);
  readonly avatarSrc = signal<string | null>(this.user()?.avatarUrl ?? null);

  readonly loggingOutAll = signal(false);

  private readonly avatarInput = viewChild<ElementRef<HTMLInputElement>>('avatarInput');

  readonly profileForm = this.formBuilder.group({
    displayName: [this.user()?.displayName ?? '', [Validators.required, Validators.maxLength(100)]],
  });

  readonly companyForm = this.formBuilder.group({
    name: [this.user()?.tenantName ?? '', [Validators.required, Validators.maxLength(100)]],
  });

  readonly passwordForm = this.formBuilder.group(
    {
      currentPassword: ['', [Validators.required]],
      newPassword: ['', [Validators.required, Validators.minLength(8), Validators.pattern(PASSWORD_PATTERN)]],
      confirmation: ['', [Validators.required]],
    },
    { validators: passwordsMatch },
  );

  saveProfile(): void {
    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      return;
    }

    this.profileSaving.set(true);
    this.profileMessage.set(null);
    this.profileError.set(null);
    const { displayName } = this.profileForm.getRawValue();

    this.auth.updateProfile(displayName.trim()).subscribe({
      next: () => {
        this.profileMessage.set('Profil mis a jour.');
        this.profileSaving.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.profileError.set(this.readError(error, 'Mise a jour impossible.'));
        this.profileSaving.set(false);
      },
    });
  }

  saveCompany(): void {
    if (this.companyForm.invalid) {
      this.companyForm.markAllAsTouched();
      return;
    }

    this.companySaving.set(true);
    this.companyMessage.set(null);
    this.companyError.set(null);
    const { name } = this.companyForm.getRawValue();

    this.companyService.rename(name.trim()).subscribe({
      next: (tenant) => {
        this.auth.updateTenantName(tenant.name);
        this.companyForm.controls.name.setValue(tenant.name);
        this.companyMessage.set('Societe mise a jour.');
        this.companySaving.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.companyError.set(this.readError(error, 'Mise a jour impossible.'));
        this.companySaving.set(false);
      },
    });
  }

  pickAvatar(): void {
    this.avatarInput()?.nativeElement.click();
  }

  onAvatarSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }

    this.avatarUploading.set(true);
    this.avatarError.set(null);
    this.profileMessage.set(null);

    this.auth.uploadAvatar(file).subscribe({
      next: (profile) => {
        this.avatarSrc.set(profile.avatarUrl ? `${profile.avatarUrl}?t=${Date.now()}` : null);
        this.profileMessage.set('Avatar mis a jour.');
        this.avatarUploading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.avatarError.set(this.readError(error, "Envoi de l'image impossible."));
        this.avatarUploading.set(false);
      },
    });
  }

  removeAvatar(): void {
    this.avatarUploading.set(true);
    this.avatarError.set(null);

    this.auth.removeAvatar().subscribe({
      next: () => {
        this.avatarSrc.set(null);
        this.profileMessage.set('Avatar supprime.');
        this.avatarUploading.set(false);
      },
      error: () => {
        this.avatarError.set('Suppression impossible.');
        this.avatarUploading.set(false);
      },
    });
  }

  savePassword(): void {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    this.passwordSaving.set(true);
    this.passwordMessage.set(null);
    this.passwordError.set(null);
    const { currentPassword, newPassword } = this.passwordForm.getRawValue();

    this.auth.changePassword(currentPassword, newPassword).subscribe({
      next: () => {
        this.passwordForm.reset();
        this.passwordMessage.set('Mot de passe modifie. Vous serez deconnecte.');
        this.passwordSaving.set(false);
        window.setTimeout(() => void this.router.navigate(['/login']), 1500);
      },
      error: (error: HttpErrorResponse) => {
        this.passwordError.set(this.readError(error, 'Modification impossible.'));
        this.passwordSaving.set(false);
      },
    });
  }

  logoutAll(): void {
    this.loggingOutAll.set(true);
    this.auth.logoutAll().subscribe({
      next: () => void this.router.navigate(['/login']),
      error: () => {
        this.loggingOutAll.set(false);
        void this.router.navigate(['/login']);
      },
    });
  }

  private readError(error: HttpErrorResponse, fallback: string): string {
    if (error.status === 0) {
      return "Impossible de joindre le serveur.";
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

    return problem?.detail ?? problem?.title ?? fallback;
  }
}
