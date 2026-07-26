namespace OrangepuffPortal.Config.Contract;

/// <summary>Fields editable from the admin CRUD screen — used for both add and edit.</summary>
public sealed record ConfigSectionUpsertRequest(
    string SModule,
    string SSectionDesc,
    string STextCode,
    bool BtShow,
    int? ISortOrder);
