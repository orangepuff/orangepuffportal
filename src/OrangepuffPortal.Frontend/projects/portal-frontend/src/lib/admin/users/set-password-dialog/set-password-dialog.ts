import { Component, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export interface SetPasswordDialogData {
  username: string;
}

@Component({
  selector: 'lib-portal-set-password-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  templateUrl: './set-password-dialog.html',
  styleUrl: './set-password-dialog.scss'
})
export class SetPasswordDialog {
  private readonly dialogRef = inject(MatDialogRef<SetPasswordDialog, string>);
  protected readonly data = inject<SetPasswordDialogData>(MAT_DIALOG_DATA);

  protected readonly form = new FormGroup({
    newPassword: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(8)] }),
    confirmPassword: new FormControl('', { nonNullable: true, validators: [Validators.required] })
  });

  protected mismatch(): boolean {
    const { newPassword, confirmPassword } = this.form.controls;
    return confirmPassword.value.length > 0 && newPassword.value !== confirmPassword.value;
  }

  protected save(): void {
    if (this.form.invalid || this.mismatch()) {
      return;
    }

    this.dialogRef.close(this.form.controls.newPassword.value);
  }

  protected cancel(): void {
    this.dialogRef.close();
  }
}
