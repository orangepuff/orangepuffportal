import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { map } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Avatar, IdentityService } from '@orangepuff/portal-frontend-shared';
import { UserSettingsService } from './user-settings.service';

@Component({
  selector: 'lib-portal-settings-page',
  imports: [Avatar, MatButtonModule],
  templateUrl: './settings-page.html',
  styleUrl: './settings-page.scss'
})
export class SettingsPage {
  private readonly route = inject(ActivatedRoute);
  protected readonly identityService = inject(IdentityService);
  private readonly userSettingsService = inject(UserSettingsService);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly avatarVersion = signal(0);

  /** Reactive to param changes (not just `.snapshot`) since the guard lets the router reuse this component across /Users/:userId/Settings navigations. */
  protected readonly targetUserId = toSignal(
    this.route.paramMap.pipe(map((params) => params.get('userId') ?? '')),
    { initialValue: this.route.snapshot.paramMap.get('userId') ?? '' }
  );

  /** Avatar upload/remove only exist for the signed-in user's own page — there's no Bff endpoint for an admin to mutate someone else's avatar. */
  protected readonly isOwnSettings = computed(() => this.targetUserId() === this.identityService.currentUser()?.userId);

  protected onFileSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    if (!file) {
      return;
    }

    this.userSettingsService.updateAvatar(file).subscribe((result) => {
      if (result.success) {
        this.avatarVersion.update((v) => v + 1);
        this.snackBar.open('Avatar updated', 'Dismiss');
      } else {
        this.snackBar.open(`Could not update avatar: ${result.rejectionReason}`, 'Dismiss');
      }
    });
  }

  protected removeAvatar(): void {
    this.userSettingsService.updateAvatar(null).subscribe((result) => {
      if (result.success) {
        this.avatarVersion.update((v) => v + 1);
        this.snackBar.open('Avatar removed', 'Dismiss');
      } else {
        this.snackBar.open(`Could not remove avatar: ${result.rejectionReason}`, 'Dismiss');
      }
    });
  }
}
