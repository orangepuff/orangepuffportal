import { Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';

/**
 * Generic themed button (primary = mat-flat-button, secondary = mat-button). Owns no text —
 * every consuming app supplies its own label via the required `label` input.
 */
@Component({
  selector: 'lib-button',
  imports: [MatButtonModule],
  templateUrl: './button.html',
  styleUrl: './button.scss'
})
export class Button {
  readonly label = input.required<string>();
  readonly variant = input<'primary' | 'secondary'>('secondary');
  readonly type = input<'button' | 'submit'>('button');
  readonly disabled = input(false);
  readonly clicked = output<void>();

  protected onClick(): void {
    if (this.type() === 'button') {
      this.clicked.emit();
    }
  }
}
