namespace OrangepuffPortal.Config.Contract;

/// <summary>One row of [config].[ConfigSections] for the admin grid.</summary>
public sealed record ConfigSectionAdminDto(
    int Id,
    string SModule,
    string SSectionDesc,
    string STextCode,
    bool BtShow,
    int? ISortOrder);
