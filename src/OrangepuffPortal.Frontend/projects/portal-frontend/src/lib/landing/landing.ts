import { Component, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from '../auth/auth.service';
import { PORTAL_SHELL_CONFIG } from '../config/portal-shell-config';
import { DEFAULT_TAGLINE, LandingContent } from './landing-content';

@Component({
  selector: 'lib-portal-landing',
  imports: [MatButtonModule],
  templateUrl: './landing.html',
  styleUrl: './landing.scss'
})
export class Landing implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly config = inject(PORTAL_SHELL_CONFIG);

  protected readonly content: LandingContent = {
    title: this.config.appName,
    tagline: this.config.landing?.tagline ?? DEFAULT_TAGLINE,
    heroImageUrl: this.config.landing?.heroImageUrl
  };

  ngOnInit(): void {
    this.authService.checkSession().subscribe(() => {
      if (this.authService.isAuthenticated()) {
        this.router.navigateByUrl('/home');
      }
    });
  }

  protected signIn(): void {
    this.authService.login('/home');
  }
}
