import { Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { ConfirmDialog, ConfirmDialogData } from '@orangepuff/portal-frontend-shared';
import { ConfigAdminService } from '../config-admin.service';
import { CONFIG_VALUE_TYPES, ConfigItemFilter, ConfigItemRow, ConfigSection } from '../config-item';
import { ConfigSectionFormDialog, ConfigSectionFormDialogData, ConfigSectionFormDialogResult } from '../config-section-form-dialog/config-section-form-dialog';
import { ConfigItemFormDialog, ConfigItemFormDialogData, ConfigItemFormDialogResult } from '../config-item-form-dialog/config-item-form-dialog';

@Component({
  selector: 'lib-portal-config-admin-page',
  imports: [
    ReactiveFormsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatPaginatorModule,
    MatTabsModule
  ],
  templateUrl: './config-admin-page.html',
  styleUrl: './config-admin-page.scss'
})
export class ConfigAdminPage implements OnInit {
  private readonly configAdminService = inject(ConfigAdminService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly sections = signal<ConfigSection[]>([]);
  protected readonly sectionColumns = ['sModule', 'sSectionDesc', 'sTextCode', 'btShow', 'actions'];

  protected readonly items = signal<ConfigItemRow[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly itemColumns = ['sSectionDesc', 'sConfigCode', 'sConfigName', 'iConfigType', 'btShow', 'btAllowUserEdit', 'actions'];
  protected readonly pageSizeOptions = [50, 100, 200, 500];
  protected readonly configValueTypes = CONFIG_VALUE_TYPES;

  private page = 1;
  private pageSize = 50;
  private appliedFilter: ConfigItemFilter = {};

  protected readonly filterForm = new FormGroup({
    sectionId: new FormControl<number | null>(null),
    configCode: new FormControl(''),
    configName: new FormControl('')
  });

  ngOnInit(): void {
    this.reloadSections();
    this.reloadItems();
  }

  protected configTypeName(item: ConfigItemRow): string {
    return this.configValueTypes.find((t) => t.value === item.iConfigType)?.name ?? `#${item.iConfigType}`;
  }

  private reloadSections(): void {
    this.configAdminService.listSections().subscribe((sections) => this.sections.set(sections));
  }

  private reloadItems(): void {
    this.configAdminService.listConfigs(this.appliedFilter, this.page, this.pageSize).subscribe((result) => {
      this.items.set(result.items);
      this.totalCount.set(result.totalCount);
    });
  }

  protected applyFilter(): void {
    const value = this.filterForm.getRawValue();
    this.appliedFilter = {
      sectionId: value.sectionId ?? undefined,
      configCode: value.configCode || undefined,
      configName: value.configName || undefined
    };
    this.page = 1;
    this.reloadItems();
  }

  protected clearFilter(): void {
    this.filterForm.reset();
    this.appliedFilter = {};
    this.page = 1;
    this.reloadItems();
  }

  protected onPage(event: PageEvent): void {
    this.page = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.reloadItems();
  }

  protected openAddSectionDialog(): void {
    const data: ConfigSectionFormDialogData = { section: null };

    this.dialog
      .open<ConfigSectionFormDialog, ConfigSectionFormDialogData, ConfigSectionFormDialogResult>(ConfigSectionFormDialog, { data })
      .afterClosed()
      .subscribe((result) => {
        if (!result) {
          return;
        }

        this.configAdminService.addSection(result).subscribe((res) => {
          if (res.success) {
            this.reloadSections();
          } else {
            this.snackBar.open(`Could not add section: ${res.rejectionReason}`, 'Dismiss');
          }
        });
      });
  }

  protected openEditSectionDialog(section: ConfigSection): void {
    const data: ConfigSectionFormDialogData = { section };

    this.dialog
      .open<ConfigSectionFormDialog, ConfigSectionFormDialogData, ConfigSectionFormDialogResult>(ConfigSectionFormDialog, { data })
      .afterClosed()
      .subscribe((result) => {
        if (!result) {
          return;
        }

        this.configAdminService.updateSection(section.id, result).subscribe((res) => {
          if (res.success) {
            this.reloadSections();
          } else {
            this.snackBar.open(`Could not update section: ${res.rejectionReason}`, 'Dismiss');
          }
        });
      });
  }

  protected deleteSection(section: ConfigSection): void {
    const data: ConfirmDialogData = { title: 'Delete section', message: `Delete section "${section.sSectionDesc}"?`, confirmLabel: 'Delete' };

    this.dialog
      .open<ConfirmDialog, ConfirmDialogData, boolean>(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }

        this.configAdminService.deleteSection(section.id).subscribe((res) => {
          if (res.success) {
            this.reloadSections();
          } else {
            this.snackBar.open(`Could not delete section: ${res.rejectionReason}`, 'Dismiss');
          }
        });
      });
  }

  protected openAddConfigDialog(): void {
    const data: ConfigItemFormDialogData = { item: null, sections: this.sections() };

    this.dialog
      .open<ConfigItemFormDialog, ConfigItemFormDialogData, ConfigItemFormDialogResult>(ConfigItemFormDialog, { data })
      .afterClosed()
      .subscribe((result) => {
        if (!result) {
          return;
        }

        this.configAdminService.addConfig(result).subscribe((res) => {
          if (res.success) {
            this.reloadItems();
          } else {
            this.snackBar.open(`Could not add config: ${res.rejectionReason}`, 'Dismiss');
          }
        });
      });
  }

  protected openEditConfigDialog(item: ConfigItemRow): void {
    const data: ConfigItemFormDialogData = { item, sections: this.sections() };

    this.dialog
      .open<ConfigItemFormDialog, ConfigItemFormDialogData, ConfigItemFormDialogResult>(ConfigItemFormDialog, { data })
      .afterClosed()
      .subscribe((result) => {
        if (!result) {
          return;
        }

        this.configAdminService.updateConfig(item.id, result).subscribe((res) => {
          if (res.success) {
            this.reloadItems();
          } else {
            this.snackBar.open(`Could not update config: ${res.rejectionReason}`, 'Dismiss');
          }
        });
      });
  }

  protected deleteConfig(item: ConfigItemRow): void {
    const data: ConfirmDialogData = { title: 'Delete config', message: `Delete config "${item.sConfigCode}"?`, confirmLabel: 'Delete' };

    this.dialog
      .open<ConfirmDialog, ConfirmDialogData, boolean>(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }

        this.configAdminService.deleteConfig(item.id).subscribe((res) => {
          if (res.success) {
            this.reloadItems();
          } else {
            this.snackBar.open(`Could not delete config: ${res.rejectionReason}`, 'Dismiss');
          }
        });
      });
  }
}
