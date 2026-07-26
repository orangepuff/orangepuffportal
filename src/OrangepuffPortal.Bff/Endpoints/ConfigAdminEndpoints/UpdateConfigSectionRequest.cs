namespace OrangepuffPortal.Bff.Endpoints.ConfigAdminEndpoints
{
    public record UpdateConfigSectionRequest(string SModule, string SSectionDesc, string STextCode, bool BtShow, int? ISortOrder);
}
