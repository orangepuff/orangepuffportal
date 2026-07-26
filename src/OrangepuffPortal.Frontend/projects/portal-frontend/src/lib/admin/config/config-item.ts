export interface PagedResult<T> {
  items: T[];
  totalCount: number;
}

export type ConfigValueTypeName = 'String' | 'Integer' | 'Decimal' | 'Boolean';

export const CONFIG_VALUE_TYPES: { value: number; name: ConfigValueTypeName }[] = [
  { value: 0, name: 'String' },
  { value: 1, name: 'Integer' },
  { value: 2, name: 'Decimal' },
  { value: 3, name: 'Boolean' }
];

export interface ConfigSection {
  id: number;
  sModule: string;
  sSectionDesc: string;
  sTextCode: string;
  btShow: boolean;
  iSortOrder: number | null;
}

export interface ConfigSectionUpsertRequest {
  sModule: string;
  sSectionDesc: string;
  sTextCode: string;
  btShow: boolean;
  iSortOrder: number | null;
}

export interface ConfigItemRow {
  id: number;
  iSectionId: number;
  sSectionDesc: string;
  sConfigCode: string;
  sConfigName: string;
  sTextCode: string;
  iConfigType: number;
  btShow: boolean;
  btAllowUserEdit: boolean;
  iSortOrder: number | null;
  sDefaultValue: string | null;
  iDefaultValue: number | null;
  nDefaultValue: number | null;
  btDefaultValue: boolean | null;
}

export interface ConfigItemUpsertRequest {
  iSectionId: number;
  sConfigCode: string;
  sConfigName: string;
  sTextCode: string;
  iConfigType: number;
  btShow: boolean;
  btAllowUserEdit: boolean;
  iSortOrder: number | null;
  sDefaultValue: string | null;
  iDefaultValue: number | null;
  nDefaultValue: number | null;
  btDefaultValue: boolean | null;
}

export interface ConfigCatalogMutationResult {
  success: boolean;
  id: number | null;
  rejectionReason: string | null;
}

export interface ConfigItemFilter {
  sectionId?: number;
  configCode?: string;
  configName?: string;
  configType?: number;
}
