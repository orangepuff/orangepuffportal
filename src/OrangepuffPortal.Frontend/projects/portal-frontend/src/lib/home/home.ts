import { Component, inject } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { PORTAL_SHELL_CONFIG } from '../config/portal-shell-config';

@Component({
  selector: 'lib-portal-home',
  imports: [],
  templateUrl: './home.html',
  styleUrl: './home.scss'
})
export class Home {
  private readonly sanitizer = inject(DomSanitizer);
  private readonly config = inject(PORTAL_SHELL_CONFIG);

  protected readonly bodyAppUrl = this.config.bodyAppUrl;
  protected readonly safeBodyAppUrl: SafeResourceUrl | null = this.bodyAppUrl
    ? this.sanitizer.bypassSecurityTrustResourceUrl(this.bodyAppUrl)
    : null;
}
