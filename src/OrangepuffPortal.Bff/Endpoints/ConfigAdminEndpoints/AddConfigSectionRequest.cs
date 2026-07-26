namespace OrangepuffPortal.Bff.Endpoints.ConfigAdminEndpoints
{
    public record AddConfigSectionRequest(string SModule, string SSectionDesc, string STextCode, bool BtShow, int? ISortOrder);
}
