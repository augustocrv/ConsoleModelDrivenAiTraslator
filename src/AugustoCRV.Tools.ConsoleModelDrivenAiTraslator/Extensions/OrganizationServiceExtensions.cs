
namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Extensions;

public sealed record DataverseSolutionInfo(string UniqueName, string FriendlyName);
public sealed record DataverseLanguageInfo(int Lcid, string DisplayName);

/// <summary>Class description.</summary>

public static class OrganizationServiceExtensions
{
    public static List<string> GetEntityLogicalNamesFromSolution(this IOrganizationService service, Guid solutionId, bool enableManaged = true)
    {
        var logicalNames = new List<string>();

        var query = new QueryExpression("solutioncomponent")
        {
            ColumnSet = new ColumnSet("objectid", "componenttype"),
            Criteria =
            {
                Conditions =
                {
                    new ConditionExpression("solutionid", ConditionOperator.Equal, solutionId),
                    new ConditionExpression("componenttype", ConditionOperator.Equal, 1)
                }
            }
        };

        var components = service.RetrieveMultiple(query);

        foreach (var component in components.Entities)
        {
            if (!component.Attributes.Contains("objectid"))
            {
                continue;
            }

            var entityId = (Guid)component["objectid"];
            var entityMetadataRequest = new Microsoft.Xrm.Sdk.Messages.RetrieveEntityRequest
            {
                EntityFilters = Microsoft.Xrm.Sdk.Metadata.EntityFilters.Entity,
                MetadataId = entityId
            };

            var response = (Microsoft.Xrm.Sdk.Messages.RetrieveEntityResponse)service.Execute(entityMetadataRequest);
            logicalNames.Add(response.EntityMetadata.LogicalName);
        }

        return logicalNames;
    }

    public static Guid GetSolutionId(this IOrganizationService service, string solutionUniqueName)
    {
        var query = new QueryExpression("solution")
        {
            ColumnSet = new ColumnSet("solutionid"),
            Criteria =
            {
                Conditions =
                {
                    new ConditionExpression("uniquename", ConditionOperator.Equal, solutionUniqueName)
                }
            }
        };

        var solutions = service.RetrieveMultiple(query);
        if (solutions.Entities.Count == 0)
        {
            throw new Exception($"Solution '{solutionUniqueName}' not found.");
        }

        return solutions.Entities[0].Id;
    }

    public static List<Guid> GetSolutionComponentObjectIds(this IOrganizationService service, Guid solutionId, int componentType)
    {
        return service.RetrieveMultiple(new QueryExpression("solutioncomponent")
        {
            ColumnSet = new ColumnSet("objectid"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("componenttype", ConditionOperator.Equal, componentType),
                    new ConditionExpression("solutionid", ConditionOperator.Equal, solutionId)
                }
            }
        }).Entities.Select(e => e.GetAttributeValue<Guid>("objectid")).ToList();
    }

    public static List<DataverseSolutionInfo> GetUnmanagedSolutions(this IOrganizationService service)
    {
        var query = new QueryExpression("solution")
        {
            ColumnSet = new ColumnSet("uniquename", "friendlyname"),
            Criteria =
            {
                Conditions =
                {
                    new ConditionExpression("ismanaged", ConditionOperator.Equal, false),
                    new ConditionExpression("isvisible", ConditionOperator.Equal, true)
                }
            },
            Orders =
            {
                new OrderExpression("friendlyname", OrderType.Ascending)
            }
        };

        var solutions = service.RetrieveMultiple(query);
        return solutions.Entities
            .Select(e => new DataverseSolutionInfo(
                e.GetAttributeValue<string>("uniquename") ?? string.Empty,
                e.GetAttributeValue<string>("friendlyname") ?? string.Empty))
            .Where(s => !string.IsNullOrWhiteSpace(s.UniqueName))
            .ToList();
    }

    public static List<DataverseLanguageInfo> GetProvisionedLanguages(this IOrganizationService service)
    {
        var request = new Microsoft.Crm.Sdk.Messages.RetrieveProvisionedLanguagesRequest();
        var response = (Microsoft.Crm.Sdk.Messages.RetrieveProvisionedLanguagesResponse)service.Execute(request);
        
        var cultures = System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.AllCultures);
        var lcidToCulture = cultures
            .Where(c => c.LCID > 0)
            .GroupBy(c => c.LCID)
            .ToDictionary(g => g.Key, g => g.First());

        return response.RetrieveProvisionedLanguages
            .Select(lcid => 
            {
                var culture = lcidToCulture.TryGetValue(lcid, out var c) ? c : null;
                var displayName = culture != null 
                    ? $"{culture.DisplayName} ({lcid})"
                    : lcid.ToString();
                return new DataverseLanguageInfo(lcid, displayName);
            })
            .OrderBy(x => x.DisplayName)
            .ToList();
    }
}

