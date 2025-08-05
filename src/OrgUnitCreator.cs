using Google.Apis.Admin.Directory.directory_v1;
using Google.Apis.Admin.Directory.directory_v1.Data;

namespace DeviceAssigner;

public class OrgUnitCreator(DirectoryService service, string customerId = Config.DefaultCustomerId)
{
    public async Task EnsureOrgUnitExistsAsync(string orgUnitPath)
    {
        var existingOUs = await GetAllOrgUnitsAsync();
        var existingPaths = new HashSet<string>(existingOUs.Select(ou => ou.OrgUnitPath), StringComparer.OrdinalIgnoreCase);

        var parts = orgUnitPath.Trim('/').Split('/');
        var currentPath = "";
        var parentPath = "/";

        foreach (var part in parts)
        {
            currentPath += "/" + part;

            if (!existingPaths.Contains(currentPath))
            {
                var newOU = new OrgUnit
                {
                    Name = part,
                    //OrgUnitPath = currentPath,
                    ParentOrgUnitPath = parentPath,
                    Description = "Test",
                    Kind = "admin#directory#orgUnit",
                };

                try
                {
                    await service.Orgunits.Insert(newOU, customerId).ExecuteAsync();
                    existingPaths.Add(currentPath);
                    Console.WriteLine($"Created OU: {currentPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex}");
                }
            }

            parentPath = currentPath;
        }
    }

    private async Task<IList<OrgUnit>> GetAllOrgUnitsAsync()
    {
        var request = service.Orgunits.List(customerId);
        request.Type = OrgunitsResource.ListRequest.TypeEnum.All;
        request.OrgUnitPath = "/";

        var response = await request.ExecuteAsync();
        return response.OrganizationUnits ?? [];
    }
}
