import { Component } from '@angular/core';
import { TranslatePipe } from '../../translation/translate.pipe';

@Component({
  selector: 'lib-portal-theme-page',
  imports: [TranslatePipe],
  templateUrl: './theme-page.html',
  styleUrl: './theme-page.scss'
})
export class ThemePage {
  protected readonly module = 'OrangepuffPortal.Frontend';
}
