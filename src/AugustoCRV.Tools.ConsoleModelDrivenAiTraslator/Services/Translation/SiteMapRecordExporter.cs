namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

internal sealed class SiteMapRecordExporter : BaseRecordExporter
{
    private EntityCollection siteMaps = new();
    private readonly List<Guid> siteMapsToTranslate = new();

    /// <summary>Exports SiteMap area, group, and sub-area titles/descriptions as TranslationRecord datasets.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<TranslationRecord>> Export(
        int sourceLcid,
        IReadOnlyList<int> allLcids,
        IOrganizationService service,
        ExportSettings settings)
    {
        ArgumentNullException.ThrowIfNull(allLcids);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(settings);

        siteMaps = GetSiteMaps(settings, service);

        var sourceLcidStr = sourceLcid.ToString(CultureInfo.InvariantCulture);
        var areas = new List<TranslationRecord>();
        var groups = new List<TranslationRecord>();
        var subAreas = new List<TranslationRecord>();
        var areaRow = 1;
        var groupRow = 1;
        var subAreaRow = 1;

        foreach (var siteMap in siteMaps.Entities)
        {
            var siteMapDoc = new XmlDocument();
            siteMapDoc.LoadXml(siteMap["sitemapxml"]?.ToString() ?? string.Empty);

            var areaNodes = siteMapDoc.SelectNodes("SiteMap/Area");
            if (areaNodes is null)
            {
                continue;
            }

            foreach (XmlNode areaNode in areaNodes)
            {
                var areaId = areaNode.Attributes?["Id"]?.Value ?? string.Empty;
                var siteMapId = siteMap.Id.ToString();
                var siteMapName = siteMap.GetAttributeValue<string>("sitemapname") ?? string.Empty;

                var areaTitles = new Dictionary<int, string>();
                var areaDescriptions = new Dictionary<int, string>();

                if (settings.ExportNames)
                {
                    foreach (XmlNode titleNode in areaNode.SelectNodes("Titles/Title") ?? (XmlNodeList)(new EmptyXmlNodeList()))
                    {
                        if (int.TryParse(titleNode.Attributes?["LCID"]?.Value, out var lcid))
                        {
                            areaTitles[lcid] = titleNode.Attributes?["Title"]?.Value ?? string.Empty;
                        }
                    }
                }

                if (settings.ExportDescriptions)
                {
                    foreach (XmlNode descNode in areaNode.SelectNodes("Descriptions/Description") ?? (XmlNodeList)(new EmptyXmlNodeList()))
                    {
                        if (int.TryParse(descNode.Attributes?["LCID"]?.Value, out var lcid))
                        {
                            areaDescriptions[lcid] = descNode.Attributes?["Description"]?.Value ?? string.Empty;
                        }
                    }
                }

                if (settings.ExportNames)
                {
                    areaRow++;
                    var sourceText = areaTitles.TryGetValue(sourceLcid, out var st) ? st : string.Empty;
                    var metaJson = SerializeAreaMetadata(siteMapName, siteMapId, areaId, "Title");
                    foreach (var targetLcid in allLcids)
                    {
                        var targetText = areaTitles.TryGetValue(targetLcid, out var tt) ? tt : string.Empty;
                        areas.Add(BuildRecord("SiteMap Areas", $"SiteMap Areas|{siteMapId}|{areaId}|Title|{targetLcid}", areaRow, sourceLcidStr, targetLcid.ToString(CultureInfo.InvariantCulture), sourceText, targetText, metaJson));
                    }
                }

                if (settings.ExportDescriptions)
                {
                    areaRow++;
                    var sourceText = areaDescriptions.TryGetValue(sourceLcid, out var sd) ? sd : string.Empty;
                    var metaJson = SerializeAreaMetadata(siteMapName, siteMapId, areaId, "Description");
                    foreach (var targetLcid in allLcids)
                    {
                        var targetText = areaDescriptions.TryGetValue(targetLcid, out var td) ? td : string.Empty;
                        areas.Add(BuildRecord("SiteMap Areas", $"SiteMap Areas|{siteMapId}|{areaId}|Description|{targetLcid}", areaRow, sourceLcidStr, targetLcid.ToString(CultureInfo.InvariantCulture), sourceText, targetText, metaJson));
                    }
                }

                var groupNodes = areaNode.SelectNodes("Group");
                if (groupNodes is null)
                {
                    continue;
                }

                foreach (XmlNode groupNode in groupNodes)
                {
                    var groupId = groupNode.Attributes?["Id"]?.Value ?? string.Empty;
                    var groupTitles = new Dictionary<int, string>();
                    var groupDescriptions = new Dictionary<int, string>();

                    if (settings.ExportNames)
                    {
                        foreach (XmlNode titleNode in groupNode.SelectNodes("Titles/Title") ?? (XmlNodeList)(new EmptyXmlNodeList()))
                        {
                            if (int.TryParse(titleNode.Attributes?["LCID"]?.Value, out var lcid))
                            {
                                groupTitles[lcid] = titleNode.Attributes?["Title"]?.Value ?? string.Empty;
                            }
                        }
                    }

                    if (settings.ExportDescriptions)
                    {
                        foreach (XmlNode descNode in groupNode.SelectNodes("Descriptions/Description") ?? (XmlNodeList)(new EmptyXmlNodeList()))
                        {
                            if (int.TryParse(descNode.Attributes?["LCID"]?.Value, out var lcid))
                            {
                                groupDescriptions[lcid] = descNode.Attributes?["Description"]?.Value ?? string.Empty;
                            }
                        }
                    }

                    if (settings.ExportNames)
                    {
                        groupRow++;
                        var sourceText = groupTitles.TryGetValue(sourceLcid, out var st) ? st : string.Empty;
                        var metaJson = SerializeGroupMetadata(siteMapName, siteMapId, areaId, groupId, "Title");
                        foreach (var targetLcid in allLcids)
                        {
                            var targetText = groupTitles.TryGetValue(targetLcid, out var tt) ? tt : string.Empty;
                            groups.Add(BuildRecord("SiteMap Groups", $"SiteMap Groups|{siteMapId}|{areaId}|{groupId}|Title|{targetLcid}", groupRow, sourceLcidStr, targetLcid.ToString(CultureInfo.InvariantCulture), sourceText, targetText, metaJson));
                        }
                    }

                    if (settings.ExportDescriptions)
                    {
                        groupRow++;
                        var sourceText = groupDescriptions.TryGetValue(sourceLcid, out var sd) ? sd : string.Empty;
                        var metaJson = SerializeGroupMetadata(siteMapName, siteMapId, areaId, groupId, "Description");
                        foreach (var targetLcid in allLcids)
                        {
                            var targetText = groupDescriptions.TryGetValue(targetLcid, out var td) ? td : string.Empty;
                            groups.Add(BuildRecord("SiteMap Groups", $"SiteMap Groups|{siteMapId}|{areaId}|{groupId}|Description|{targetLcid}", groupRow, sourceLcidStr, targetLcid.ToString(CultureInfo.InvariantCulture), sourceText, targetText, metaJson));
                        }
                    }

                    var subAreaNodes = groupNode.SelectNodes("SubArea");
                    if (subAreaNodes is null)
                    {
                        continue;
                    }

                    foreach (XmlNode subAreaNode in subAreaNodes)
                    {
                        var subAreaId = subAreaNode.Attributes?["Id"]?.Value ?? string.Empty;
                        var subAreaTitles = new Dictionary<int, string>();
                        var subAreaDescriptions = new Dictionary<int, string>();

                        if (settings.ExportNames)
                        {
                            foreach (XmlNode titleNode in subAreaNode.SelectNodes("Titles/Title") ?? (XmlNodeList)(new EmptyXmlNodeList()))
                            {
                                if (int.TryParse(titleNode.Attributes?["LCID"]?.Value, out var lcid))
                                {
                                    subAreaTitles[lcid] = titleNode.Attributes?["Title"]?.Value ?? string.Empty;
                                }
                            }
                        }

                        if (settings.ExportDescriptions)
                        {
                            foreach (XmlNode descNode in subAreaNode.SelectNodes("Descriptions/Description") ?? (XmlNodeList)(new EmptyXmlNodeList()))
                            {
                                if (int.TryParse(descNode.Attributes?["LCID"]?.Value, out var lcid))
                                {
                                    subAreaDescriptions[lcid] = descNode.Attributes?["Description"]?.Value ?? string.Empty;
                                }
                            }
                        }

                        if (settings.ExportNames)
                        {
                            subAreaRow++;
                            var sourceText = subAreaTitles.TryGetValue(sourceLcid, out var st) ? st : string.Empty;
                            var metaJson = SerializeSubAreaMetadata(siteMapName, siteMapId, areaId, groupId, subAreaId, "Title");
                            foreach (var targetLcid in allLcids)
                            {
                                var targetText = subAreaTitles.TryGetValue(targetLcid, out var tt) ? tt : string.Empty;
                                subAreas.Add(BuildRecord("SiteMap SubAreas", $"SiteMap SubAreas|{siteMapId}|{areaId}|{groupId}|{subAreaId}|Title|{targetLcid}", subAreaRow, sourceLcidStr, targetLcid.ToString(CultureInfo.InvariantCulture), sourceText, targetText, metaJson));
                            }
                        }

                        if (settings.ExportDescriptions)
                        {
                            subAreaRow++;
                            var sourceText = subAreaDescriptions.TryGetValue(sourceLcid, out var sd) ? sd : string.Empty;
                            var metaJson = SerializeSubAreaMetadata(siteMapName, siteMapId, areaId, groupId, subAreaId, "Description");
                            foreach (var targetLcid in allLcids)
                            {
                                var targetText = subAreaDescriptions.TryGetValue(targetLcid, out var td) ? td : string.Empty;
                                subAreas.Add(BuildRecord("SiteMap SubAreas", $"SiteMap SubAreas|{siteMapId}|{areaId}|{groupId}|{subAreaId}|Description|{targetLcid}", subAreaRow, sourceLcidStr, targetLcid.ToString(CultureInfo.InvariantCulture), sourceText, targetText, metaJson));
                            }
                        }
                    }
                }
            }
        }

        return new Dictionary<string, IReadOnlyList<TranslationRecord>>
        {
            ["SiteMap Areas"] = areas,
            ["SiteMap Groups"] = groups,
            ["SiteMap SubAreas"] = subAreas
        };
    }

    /// <summary>Retrieves or loads site maps based on export settings.</summary>
    public EntityCollection GetSiteMaps(ExportSettings? settings, IOrganizationService service)
    {
        if (siteMaps.Entities.Count > 0)
        {
            return siteMaps;
        }

        var ec = new EntityCollection();
        var ids = new List<Guid>();

        if ((settings?.SolutionId ?? Guid.Empty) != Guid.Empty)
        {
            var components = service.RetrieveMultiple(new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("objectid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("componenttype", ConditionOperator.Equal, 62),
                        new ConditionExpression("solutionid", ConditionOperator.Equal, settings!.SolutionId)
                    }
                }
            });
            ids.AddRange(components.Entities.Select(e => e.GetAttributeValue<Guid>("objectid")));
        }
        else
        {
            var sitemapsIds = service.RetrieveMultiple(new QueryExpression("appmodulecomponent")
            {
                ColumnSet = new ColumnSet(true),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("componenttype", ConditionOperator.Equal, 62) }
                }
            });

            foreach (var siteMapId in sitemapsIds.Entities)
            {
                var appModuleRef = siteMapId.GetAttributeValue<EntityReference>("appmoduleidunique");
                if (ec.Entities.Any(ent => ent.Id == siteMapId.GetAttributeValue<Guid>("objectid"))
                    || appModuleRef?.Name == null)
                {
                    continue;
                }

                ids.Add(siteMapId.GetAttributeValue<Guid>("objectid"));
            }
        }

        foreach (var id in ids)
        {
            try
            {
                var tmpSiteMap = service.Retrieve("sitemap", id, new ColumnSet(true));
                if (settings is { EnableManaged: false } && tmpSiteMap.GetAttributeValue<bool>("ismanaged"))
                {
                    continue;
                }

                if (tmpSiteMap.GetAttributeValue<OptionSetValue>("componentstate")?.Value != 0)
                {
                    continue;
                }

                ec.Entities.Add(tmpSiteMap);
            }
            catch (Exception) { }
        }

        if (settings == null || settings.SolutionId == Guid.Empty)
        {
            var qe = new QueryExpression("sitemap") { ColumnSet = new ColumnSet(true) };
            if (settings is { EnableManaged: false })
            {
                qe.Criteria.AddCondition("ismanaged", ConditionOperator.Equal, false);
            }

            var ecDefault = service.RetrieveMultiple(qe);
            if (ecDefault.Entities.Count > 0)
            {
                ecDefault.Entities.First()["sitemapname"] = "Default";
                ec.Entities.Add(ecDefault.Entities.First());
            }
        }

        siteMaps = ec;
        return siteMaps;
    }

    /// <summary>Applies translated SiteMap data to the service.</summary>
    public void Import(IOrganizationService service)
    {
        var arg = new TranslationProgressEventArgs { SheetName = "SiteMaps" };

        foreach (var siteMap in siteMaps.Entities)
        {
            if (siteMapsToTranslate.Contains(siteMap.Id))
            {
                AddRequest(new UpdateRequest { Target = siteMap });
                ExecuteMultiple(service, arg, siteMaps.Entities.Count);
            }
        }

        ExecuteMultiple(service, arg, siteMaps.Entities.Count, true);
    }

    /// <summary>Applies translated area data from the given table into the sitemap XML.</summary>
    public void PrepareAreas(TranslationTable sheet, IOrganizationService service, bool forceUpdate)
    {
        GetSiteMaps(null, service);

        foreach (var siteMap in siteMaps.Entities)
        {
            var siteMapDoc = new XmlDocument();
            siteMapDoc.LoadXml(siteMap["sitemapxml"]?.ToString() ?? string.Empty);

            var rowsCount = sheet.Dimension?.Rows ?? 0;
            var cellsCount = GetLastLcidColumnCount(sheet);

            for (var rowI = 1; rowI < rowsCount; rowI++)
            {
                if (HasEmptyRequiredCells(sheet, rowI, 3))
                {
                    continue;
                }

                if (sheet.GetCell(rowI + 1, 1).Value == null)
                {
                    break;
                }

                if (sheet.GetCell(rowI + 1, 2).Value?.ToString() != siteMap.Id.ToString())
                {
                    continue;
                }

                if (!siteMapsToTranslate.Contains(siteMap.Id))
                {
                    siteMapsToTranslate.Add(siteMap.Id);
                }

                var areaId = sheet.GetCell(rowI + 1, 3).Value?.ToString() ?? string.Empty;
                var areaNode = siteMapDoc.SelectSingleNode("SiteMap/Area[@Id='" + areaId + "']");
                if (areaNode == null)
                {
                    continue;
                }

                var typeValue = sheet.GetCell(rowI + 1, 4).Value?.ToString() ?? string.Empty;
                var columnIndex = 4;
                while (columnIndex < cellsCount)
                {
                    var cellValue = sheet.GetCell(rowI + 1, columnIndex + 1).Value;
                    if (cellValue != null)
                    {
                        var lcidStr = sheet.GetCell(1, columnIndex + 1).Value?.ToString() ?? string.Empty;
                        var label = cellValue.ToString() ?? string.Empty;

                        if (!int.TryParse(lcidStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lcid))
                        {
                            columnIndex++;
                            continue;
                        }

                        if (!forceUpdate && !IsTranslationUpdated(sheet, rowI, lcid, label))
                        {
                            columnIndex++;
                            continue;
                        }

                        if (typeValue == "Title")
                        {
                            UpdateXmlNode(areaNode, "Titles", "Title", lcidStr, label);
                        }
                        else
                        {
                            UpdateXmlNode(areaNode, "Descriptions", "Description", lcidStr, label);
                        }
                    }

                    columnIndex++;
                }
            }

            siteMap["sitemapxml"] = siteMapDoc.OuterXml;
        }
    }

    /// <summary>Applies translated group data from the given table into the sitemap XML.</summary>
    public void PrepareGroups(TranslationTable sheet, IOrganizationService service, bool forceUpdate)
    {
        GetSiteMaps(null, service);

        foreach (var siteMap in siteMaps.Entities)
        {
            var siteMapDoc = new XmlDocument();
            siteMapDoc.LoadXml(siteMap["sitemapxml"]?.ToString() ?? string.Empty);

            var rowsCount = sheet.Dimension?.Rows ?? 0;
            var cellsCount = GetLastLcidColumnCount(sheet);

            for (var rowI = 1; rowI < rowsCount; rowI++)
            {
                if (HasEmptyRequiredCells(sheet, rowI, 4))
                {
                    continue;
                }

                if (sheet.GetCell(rowI + 1, 1).Value == null)
                {
                    break;
                }

                if (sheet.GetCell(rowI + 1, 2).Value?.ToString() != siteMap.Id.ToString())
                {
                    continue;
                }

                if (!siteMapsToTranslate.Contains(siteMap.Id))
                {
                    siteMapsToTranslate.Add(siteMap.Id);
                }

                var areaId = sheet.GetCell(rowI + 1, 3).Value?.ToString() ?? string.Empty;
                var groupId = sheet.GetCell(rowI + 1, 4).Value?.ToString() ?? string.Empty;
                var groupNode = siteMapDoc.SelectSingleNode(
                    "SiteMap/Area[@Id='" + areaId + "']/Group[@Id='" + groupId + "']");
                if (groupNode == null)
                {
                    continue;
                }

                var typeValue = sheet.GetCell(rowI + 1, 5).Value?.ToString() ?? string.Empty;
                var columnIndex = 5;
                while (columnIndex < cellsCount)
                {
                    var cellValue = sheet.GetCell(rowI + 1, columnIndex + 1).Value;
                    if (cellValue != null)
                    {
                        var lcidStr = sheet.GetCell(1, columnIndex + 1).Value?.ToString() ?? string.Empty;
                        var label = cellValue.ToString() ?? string.Empty;

                        if (!int.TryParse(lcidStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lcid))
                        {
                            columnIndex++;
                            continue;
                        }

                        if (!forceUpdate && !IsTranslationUpdated(sheet, rowI, lcid, label))
                        {
                            columnIndex++;
                            continue;
                        }

                        if (typeValue == "Title")
                        {
                            UpdateXmlNode(groupNode, "Titles", "Title", lcidStr, label);
                        }
                        else
                        {
                            UpdateXmlNode(groupNode, "Descriptions", "Description", lcidStr, label);
                        }
                    }

                    columnIndex++;
                }
            }

            siteMap["sitemapxml"] = siteMapDoc.OuterXml;
        }
    }

    /// <summary>Applies translated sub-area data from the given table into the sitemap XML.</summary>
    public void PrepareSubAreas(TranslationTable sheet, IOrganizationService service, bool forceUpdate)
    {
        GetSiteMaps(null, service);

        foreach (var siteMap in siteMaps.Entities)
        {
            var siteMapDoc = new XmlDocument();
            siteMapDoc.LoadXml(siteMap["sitemapxml"]?.ToString() ?? string.Empty);

            var rowsCount = sheet.Dimension?.Rows ?? 0;
            var cellsCount = GetLastLcidColumnCount(sheet);

            for (var rowI = 1; rowI < rowsCount; rowI++)
            {
                if (HasEmptyRequiredCells(sheet, rowI, 5))
                {
                    continue;
                }

                if (sheet.GetCell(rowI + 1, 1).Value == null)
                {
                    break;
                }

                if (sheet.GetCell(rowI + 1, 2).Value?.ToString() != siteMap.Id.ToString())
                {
                    continue;
                }

                if (!siteMapsToTranslate.Contains(siteMap.Id))
                {
                    siteMapsToTranslate.Add(siteMap.Id);
                }

                var areaId = sheet.GetCell(rowI + 1, 3).Value?.ToString() ?? string.Empty;
                var groupId = sheet.GetCell(rowI + 1, 4).Value?.ToString() ?? string.Empty;
                var subAreaId = sheet.GetCell(rowI + 1, 5).Value?.ToString() ?? string.Empty;
                var subAreaNode = siteMapDoc.SelectSingleNode(
                    "SiteMap/Area[@Id='" + areaId + "']/Group[@Id='" + groupId + "']/SubArea[@Id='" + subAreaId + "']");
                if (subAreaNode == null)
                {
                    continue;
                }

                var typeValue = sheet.GetCell(rowI + 1, 6).Value?.ToString() ?? string.Empty;
                var columnIndex = 6;
                while (columnIndex < cellsCount)
                {
                    var cellValue = sheet.GetCell(rowI + 1, columnIndex + 1).Value;
                    if (cellValue != null)
                    {
                        var lcidStr = sheet.GetCell(1, columnIndex + 1).Value?.ToString() ?? string.Empty;
                        var label = cellValue.ToString() ?? string.Empty;

                        if (!int.TryParse(lcidStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lcid))
                        {
                            columnIndex++;
                            continue;
                        }

                        if (!forceUpdate && !IsTranslationUpdated(sheet, rowI, lcid, label))
                        {
                            columnIndex++;
                            continue;
                        }

                        if (typeValue == "Title")
                        {
                            UpdateXmlNode(subAreaNode, "Titles", "Title", lcidStr, label);
                        }
                        else
                        {
                            UpdateXmlNode(subAreaNode, "Descriptions", "Description", lcidStr, label);
                        }
                    }

                    columnIndex++;
                }
            }

            siteMap["sitemapxml"] = siteMapDoc.OuterXml;
        }
    }

    // Returns the exclusive upper-bound column index for LCID columns (0-based).
    private static int GetLastLcidColumnCount(TranslationTable sheet)
    {
        var cols = sheet.Dimension?.Columns ?? 0;
        var last = -1;
        for (var c = 0; c < cols; c++)
        {
            var header = sheet.GetCell(1, c + 1).Value?.ToString();
            if (int.TryParse(header, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                last = c;
            }
        }

        return last + 1;
    }

    private static bool HasEmptyRequiredCells(TranslationTable sheet, int rowI, int count)
    {
        for (var c = 0; c < count; c++)
        {
            if (sheet.GetCell(rowI + 1, c + 1).Value == null)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsTranslationUpdated(TranslationTable sheet, int rowI, int lcid, string label)
    {
        var checksumHeader = $"Checksum_{lcid}";
        var cols = sheet.Dimension?.Columns ?? 0;
        for (var c = 0; c < cols; c++)
        {
            var header = sheet.GetCell(1, c + 1).Value?.ToString();
            if (string.Equals(header, checksumHeader, StringComparison.OrdinalIgnoreCase))
            {
                var stored = sheet.GetCell(rowI + 1, c + 1).Value?.ToString() ?? string.Empty;
                return !stored.Equals(CalculateChecksum(label), StringComparison.OrdinalIgnoreCase);
            }
        }

        return true;
    }

    private TranslationRecord BuildRecord(
        string dataset, string key, int rowNumber,
        string sourceLcidStr, string targetLcidStr,
        string sourceText, string targetText, string metadataJson)
    {
        return new TranslationRecord
        {
            Dataset = dataset,
            RecordKey = key,
            RowNumber = rowNumber,
            SourceLcid = sourceLcidStr,
            TargetLcid = targetLcidStr,
            SourceText = sourceText,
            TargetText = targetText,
            Checksum = string.IsNullOrEmpty(targetText) ? string.Empty : CalculateChecksum(targetText),
            MetadataJson = metadataJson
        };
    }

    private static string SerializeAreaMetadata(string siteMapName, string siteMapId, string areaId, string type)
        => JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["SiteMap Name"] = siteMapName,
            ["SiteMap Id"] = siteMapId,
            ["Area Id"] = areaId,
            ["Type"] = type
        });

    private static string SerializeGroupMetadata(string siteMapName, string siteMapId, string areaId, string groupId, string type)
        => JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["SiteMap Name"] = siteMapName,
            ["SiteMap Id"] = siteMapId,
            ["Area Id"] = areaId,
            ["Group Id"] = groupId,
            ["Type"] = type
        });

    private static string SerializeSubAreaMetadata(string siteMapName, string siteMapId, string areaId, string groupId, string subAreaId, string type)
        => JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["SiteMap Name"] = siteMapName,
            ["SiteMap Id"] = siteMapId,
            ["Area Id"] = areaId,
            ["Group Id"] = groupId,
            ["SubArea Id"] = subAreaId,
            ["Type"] = type
        });

    private void UpdateXmlNode(XmlNode node, string collectionName, string itemName, string? lcid, string? description)
    {
        var safeLcid = lcid ?? string.Empty;
        var safeDescription = description ?? string.Empty;
        var ownerDocument = node.OwnerDocument;
        if (ownerDocument == null)
        {
            return;
        }

        XmlNode? refNode;
        if (collectionName == "Titles" && node.FirstChild != null)
        {
            refNode = node.FirstChild;
        }
        else
        {
            refNode = node.Name switch
            {
                "Area" => node.SelectSingleNode("Group"),
                "Group" => node.SelectSingleNode("SubArea"),
                "SubArea" => node.SelectSingleNode("Privilege"),
                _ => null
            };
        }

        var labelsNode = node.SelectSingleNode(collectionName);
        if (labelsNode == null)
        {
            labelsNode = ownerDocument.CreateElement(collectionName);
            if (refNode != null)
            {
                node.InsertBefore(labelsNode, refNode);
            }
            else
            {
                node.AppendChild(labelsNode);
            }
        }

        var labelNode = labelsNode.SelectSingleNode($"{itemName}[@LCID='{safeLcid}']");
        if (labelNode == null)
        {
            labelNode = ownerDocument.CreateElement(itemName);
            labelsNode.AppendChild(labelNode);

            var languageAttr = ownerDocument.CreateAttribute("LCID");
            languageAttr.Value = safeLcid;
            labelNode.Attributes!.Append(languageAttr);
            labelNode.Attributes.Append(ownerDocument.CreateAttribute(itemName));
        }

        var descriptionAttribute = labelNode.Attributes?[itemName];
        if (descriptionAttribute == null)
        {
            descriptionAttribute = ownerDocument.CreateAttribute(itemName);
            labelNode.Attributes!.Append(descriptionAttribute);
        }

        descriptionAttribute.Value = safeDescription;
    }
}

// Minimal helper to avoid null reference when SelectNodes returns null.
file sealed class EmptyXmlNodeList : XmlNodeList
{
    public override int Count => 0;
    public override XmlNode Item(int index) => throw new ArgumentOutOfRangeException(nameof(index));
    public override System.Collections.IEnumerator GetEnumerator() => Enumerable.Empty<XmlNode>().GetEnumerator();
}
