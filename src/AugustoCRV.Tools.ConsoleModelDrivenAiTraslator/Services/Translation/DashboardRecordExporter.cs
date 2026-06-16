namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

internal sealed class DashboardRecordExporter : BaseRecordExporter
{
    private const string DatasetDashboards = "Dashboards";
    private const string DatasetTabs = "Dashboards Tabs";
    private const string DatasetSections = "Dashboards Sections";
    private const string DatasetFields = "Dashboards Fields";

    /// <summary>Exports dashboard names/descriptions and dashboard content (tabs, sections, fields) as TranslationRecord objects.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<TranslationRecord>> Export(
        int sourceLcid,
        IReadOnlyList<int> allLcids,
        IOrganizationService service,
        ExportSettings settings)
    {
        ArgumentNullException.ThrowIfNull(allLcids);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(settings);

        var sourceLcidStr = sourceLcid.ToString(CultureInfo.InvariantCulture);

        var ids = new List<Guid>();
        if (settings.SolutionId != Guid.Empty)
        {
            ids = service.GetSolutionComponentObjectIds(settings.SolutionId, 60);
        }

        var userSetting = GetCurrentUserSettings(service);
        var userSettingLcid = userSetting.GetAttributeValue<int>("uilanguageid");
        if (userSettingLcid == 0) userSettingLcid = allLcids.Count > 0 ? allLcids[0] : sourceLcid;
        var currentSetting = userSettingLcid;

        var crmForms = new List<CrmForm>();
        var crmFormTabs = new List<CrmFormTab>();
        var crmFormSections = new List<CrmFormSection>();
        var crmFormLabels = new List<CrmFormLabel>();

        // Collect dashboard XML-based content (tabs, sections, fields) for each language
        foreach (var lcid in allLcids)
        {
            if (currentSetting != lcid)
            {
                userSetting["localeid"] = lcid;
                userSetting["uilanguageid"] = lcid;
                userSetting["helplanguageid"] = lcid;
                service.Update(userSetting);
                currentSetting = lcid;
            }

            var forms = RetrieveDashboardList(ids, service, settings.EnableManaged);
            foreach (var form in forms)
            {
                var sFormXml = form.GetAttributeValue<string>("formxml");
                var formXml = new XmlDocument();
                formXml.LoadXml(sFormXml ?? string.Empty);

                var tabNodes = formXml.SelectNodes("//tab");
                if (tabNodes == null) continue;

                foreach (XmlNode tabNode in tabNodes)
                {
                    var tabName = ExtractTabName(tabNode, lcid, crmFormTabs, form);
                    var sectionNodes = tabNode.SelectNodes("columns/column/sections/section");
                    if (sectionNodes == null) continue;

                    foreach (XmlNode sectionNode in sectionNodes)
                    {
                        var sectionName = ExtractSection(sectionNode, lcid, crmFormSections, form, tabName);
                        var labelNodes = sectionNode.SelectNodes("rows/row/cell");
                        if (labelNodes == null) continue;

                        foreach (XmlNode labelNode in labelNodes)
                        {
                            ExtractField(labelNode, crmFormLabels, form, tabName, sectionName, lcid);
                        }
                    }
                }
            }
        }

        if (userSettingLcid != currentSetting)
        {
            userSetting["localeid"] = userSettingLcid;
            userSetting["uilanguageid"] = userSettingLcid;
            userSetting["helplanguageid"] = userSettingLcid;
            service.Update(userSetting);
        }

        // Collect localized dashboard names/descriptions via RetrieveLocLabelsRequest
        var forms2 = RetrieveDashboardList(ids, service, settings.EnableManaged);
        foreach (var form in forms2)
        {
            var crmForm = crmForms.FirstOrDefault(f => f.FormUniqueId == form.GetAttributeValue<Guid>("formidunique"));
            if (crmForm == null)
            {
                crmForm = new CrmForm
                {
                    FormUniqueId = form.GetAttributeValue<Guid>("formidunique"),
                    Id = form.GetAttributeValue<Guid>("formid"),
                    Names = new Dictionary<int, string>(),
                    Descriptions = new Dictionary<int, string>()
                };
                crmForms.Add(crmForm);
            }

            if (settings.ExportNames)
            {
                var response = (RetrieveLocLabelsResponse)service.Execute(new RetrieveLocLabelsRequest
                {
                    AttributeName = "name",
                    EntityMoniker = new EntityReference("systemform", form.Id)
                });
                foreach (var locLabel in response.Label.LocalizedLabels)
                {
                    crmForm.Names.TryAdd(locLabel.LanguageCode, locLabel.Label);
                }
            }

            if (settings.ExportDescriptions)
            {
                var response = (RetrieveLocLabelsResponse)service.Execute(new RetrieveLocLabelsRequest
                {
                    AttributeName = "description",
                    EntityMoniker = new EntityReference("systemform", form.Id)
                });
                foreach (var locLabel in response.Label.LocalizedLabels)
                {
                    crmForm.Descriptions.TryAdd(locLabel.LanguageCode, locLabel.Label);
                }
            }
        }

        // Build TranslationRecord collections
        var dashboardsRecords = new List<TranslationRecord>();
        var tabsRecords = new List<TranslationRecord>();
        var sectionsRecords = new List<TranslationRecord>();
        var fieldsRecords = new List<TranslationRecord>();

        var formRowNumber = 2;
        foreach (var crmForm in crmForms)
        {
            var formUniqueId = crmForm.FormUniqueId.ToString("B");
            var formId = crmForm.Id.ToString("B");

            if (settings.ExportNames)
            {
                var sourceText = crmForm.Names.TryGetValue(sourceLcid, out var sn) ? sn : string.Empty;
                var rowNumber = formRowNumber++;
                var metadataJson = SerializeDashboardsMetadata(formUniqueId, formId, "Name");
                foreach (var targetLcid in allLcids)
                {
                    var targetLcidStr = targetLcid.ToString(CultureInfo.InvariantCulture);
                    var targetText = crmForm.Names.TryGetValue(targetLcid, out var tn) ? tn : string.Empty;
                    dashboardsRecords.Add(new TranslationRecord
                    {
                        Dataset = DatasetDashboards,
                        RecordKey = $"{DatasetDashboards}|{formUniqueId}|Name|{targetLcidStr}",
                        RowNumber = rowNumber,
                        SourceLcid = sourceLcidStr,
                        TargetLcid = targetLcidStr,
                        SourceText = sourceText,
                        TargetText = targetText,
                        Checksum = string.IsNullOrEmpty(targetText) ? string.Empty : CalculateChecksum(targetText),
                        MetadataJson = metadataJson
                    });
                }
            }

            if (settings.ExportDescriptions)
            {
                var sourceText = crmForm.Descriptions.TryGetValue(sourceLcid, out var sd) ? sd : string.Empty;
                var rowNumber = formRowNumber++;
                var metadataJson = SerializeDashboardsMetadata(formUniqueId, formId, "Description");
                foreach (var targetLcid in allLcids)
                {
                    var targetLcidStr = targetLcid.ToString(CultureInfo.InvariantCulture);
                    var targetText = crmForm.Descriptions.TryGetValue(targetLcid, out var td) ? td : string.Empty;
                    dashboardsRecords.Add(new TranslationRecord
                    {
                        Dataset = DatasetDashboards,
                        RecordKey = $"{DatasetDashboards}|{formUniqueId}|Description|{targetLcidStr}",
                        RowNumber = rowNumber,
                        SourceLcid = sourceLcidStr,
                        TargetLcid = targetLcidStr,
                        SourceText = sourceText,
                        TargetText = targetText,
                        Checksum = string.IsNullOrEmpty(targetText) ? string.Empty : CalculateChecksum(targetText),
                        MetadataJson = metadataJson
                    });
                }
            }
        }

        var tabRowNumber = 2;
        foreach (var crmFormTab in crmFormTabs)
        {
            var tabId = crmFormTab.Id.ToString("B");
            var formUniqueId = crmFormTab.FormUniqueId.ToString("B");
            var formId = crmFormTab.FormId.ToString("B");
            var sourceText = crmFormTab.Names.TryGetValue(sourceLcid, out var stn) ? stn : string.Empty;
            var rowNumber = tabRowNumber++;
            var metadataJson = SerializeDashboardTabsMetadata(tabId, crmFormTab.Form, formUniqueId, formId);
            foreach (var targetLcid in allLcids)
            {
                var targetLcidStr = targetLcid.ToString(CultureInfo.InvariantCulture);
                var targetText = crmFormTab.Names.TryGetValue(targetLcid, out var tn) ? tn : string.Empty;
                tabsRecords.Add(new TranslationRecord
                {
                    Dataset = DatasetTabs,
                    RecordKey = $"{DatasetTabs}|{tabId}|{targetLcidStr}",
                    RowNumber = rowNumber,
                    SourceLcid = sourceLcidStr,
                    TargetLcid = targetLcidStr,
                    SourceText = sourceText,
                    TargetText = targetText,
                    Checksum = string.IsNullOrEmpty(targetText) ? string.Empty : CalculateChecksum(targetText),
                    MetadataJson = metadataJson
                });
            }
        }

        var sectionRowNumber = 2;
        foreach (var crmFormSection in crmFormSections)
        {
            var sectionId = crmFormSection.Id.ToString("B");
            var formUniqueId = crmFormSection.FormUniqueId.ToString("B");
            var formId = crmFormSection.FormId.ToString("B");
            var sourceText = crmFormSection.Names.TryGetValue(sourceLcid, out var ssn) ? ssn : string.Empty;
            var rowNumber = sectionRowNumber++;
            var metadataJson = SerializeDashboardSectionsMetadata(sectionId, crmFormSection.Form, formUniqueId, formId, crmFormSection.Tab);
            foreach (var targetLcid in allLcids)
            {
                var targetLcidStr = targetLcid.ToString(CultureInfo.InvariantCulture);
                var targetText = crmFormSection.Names.TryGetValue(targetLcid, out var tn) ? tn : string.Empty;
                sectionsRecords.Add(new TranslationRecord
                {
                    Dataset = DatasetSections,
                    RecordKey = $"{DatasetSections}|{sectionId}|{targetLcidStr}",
                    RowNumber = rowNumber,
                    SourceLcid = sourceLcidStr,
                    TargetLcid = targetLcidStr,
                    SourceText = sourceText,
                    TargetText = targetText,
                    Checksum = string.IsNullOrEmpty(targetText) ? string.Empty : CalculateChecksum(targetText),
                    MetadataJson = metadataJson
                });
            }
        }

        var fieldRowNumber = 2;
        foreach (var crmFormLabel in crmFormLabels)
        {
            var labelId = crmFormLabel.Id.ToString("B");
            var formUniqueId = crmFormLabel.FormUniqueId.ToString("B");
            var formId = crmFormLabel.FormId.ToString("B");
            var sourceText = crmFormLabel.Names.TryGetValue(sourceLcid, out var sfn) ? sfn : string.Empty;
            var rowNumber = fieldRowNumber++;
            var metadataJson = SerializeDashboardFieldsMetadata(labelId, crmFormLabel.Form, formUniqueId, formId, crmFormLabel.Tab, crmFormLabel.Section, crmFormLabel.Attribute);
            foreach (var targetLcid in allLcids)
            {
                var targetLcidStr = targetLcid.ToString(CultureInfo.InvariantCulture);
                var targetText = crmFormLabel.Names.TryGetValue(targetLcid, out var ftn) ? ftn : string.Empty;
                fieldsRecords.Add(new TranslationRecord
                {
                    Dataset = DatasetFields,
                    RecordKey = $"{DatasetFields}|{labelId}|{targetLcidStr}",
                    RowNumber = rowNumber,
                    SourceLcid = sourceLcidStr,
                    TargetLcid = targetLcidStr,
                    SourceText = sourceText,
                    TargetText = targetText,
                    Checksum = string.IsNullOrEmpty(targetText) ? string.Empty : CalculateChecksum(targetText),
                    MetadataJson = metadataJson
                });
            }
        }

        var result = new Dictionary<string, IReadOnlyList<TranslationRecord>>();
        if (dashboardsRecords.Count > 0) result[DatasetDashboards] = dashboardsRecords;
        if (tabsRecords.Count > 0) result[DatasetTabs] = tabsRecords;
        if (sectionsRecords.Count > 0) result[DatasetSections] = sectionsRecords;
        if (fieldsRecords.Count > 0) result[DatasetFields] = fieldsRecords;
        return result;
    }

    /// <summary>Imports localized dashboard names and descriptions from a TranslationTable.</summary>
    public void ImportFormName(TranslationTable sheet, IOrganizationService service, bool forceUpdate)
    {
        var dimension = sheet.Dimension;
        if (dimension is null)
        {
            return;
        }

        var rowsCount = dimension.Rows;
        var cellsCount = GetLastLcidColumnCount(sheet);
        var requests = new List<SetLocLabelsRequest>();
        var needUpdate = new List<Guid>();

        for (var rowI = 1; rowI < rowsCount; rowI++)
        {
            var formIdValue = sheet.GetCell(rowI + 1, 2).Value?.ToString();
            if (!Guid.TryParse(formIdValue, out var currentFormId)) continue;

            var typeValue = sheet.GetCell(rowI + 1, 3).Value?.ToString();
            var request = new SetLocLabelsRequest
            {
                RequestId = Guid.NewGuid(),
                EntityMoniker = new EntityReference("systemform", currentFormId),
                AttributeName = typeValue == "Name" ? "name" : "description"
            };

            var labels = new List<LocalizedLabel>();
            var columnIndex = 3;
            while (columnIndex < cellsCount)
            {
                if (sheet.GetCell(rowI + 1, columnIndex + 1).Value != null)
                {
                    var lcidValue = sheet.GetCell(1, columnIndex + 1).Value?.ToString();
                    if (!int.TryParse(lcidValue, out var lcid))
                    {
                        columnIndex++;
                        continue;
                    }

                    var label = sheet.GetCell(rowI + 1, columnIndex + 1).Value?.ToString() ?? string.Empty;

                    if (!forceUpdate && !IsTranslationUpdated(sheet, rowI, lcid, label))
                    {
                        columnIndex++;
                        continue;
                    }

                    labels.Add(new LocalizedLabel(label, lcid));
                }

                columnIndex++;
            }

            request.Labels = labels.ToArray();

            if (request.RequestId.HasValue && !needUpdate.Contains(request.RequestId.Value))
            {
                needUpdate.Add(request.RequestId.Value);
            }

            requests.Add(request);
        }

        var arg = new TranslationProgressEventArgs { SheetName = sheet.Name };
        var requestsNeedingUpdate = requests.Where(r => r.RequestId.HasValue && needUpdate.Contains(r.RequestId.Value)).ToList();
        foreach (var request in requestsNeedingUpdate)
        {
            AddRequest(request);
            ExecuteMultiple(service, arg, requestsNeedingUpdate.Count);
        }
        ExecuteMultiple(service, arg, requestsNeedingUpdate.Count, true);
    }

    /// <summary>Pushes updated dashboard XML content back to Dataverse.</summary>
    public void ImportFormsContent(IOrganizationService service, List<Entity> forms)
    {
        var arg = new TranslationProgressEventArgs { SheetName = "Dashboards" };
        foreach (var form in forms)
        {
            AddRequest(new UpdateRequest { Target = form });
            ExecuteMultiple(service, arg, forms.Count);
        }
        ExecuteMultiple(service, arg, forms.Count, true);
    }

    /// <summary>Applies translated tab labels from a TranslationTable into the dashboard XML of each affected dashboard.</summary>
    public void PrepareFormTabs(TranslationTable sheet, IOrganizationService service, List<Entity> forms, bool forceUpdate)
    {
        var dimension = sheet.Dimension;
        if (dimension is null)
        {
            return;
        }

        var rowsCount = dimension.Rows;
        var cellsCount = GetLastLcidColumnCount(sheet);

        for (var rowI = 1; rowI < rowsCount; rowI++)
        {
            var tabId = sheet.GetCell(rowI + 1, 1).Value?.ToString();
            var formIdValue = sheet.GetCell(rowI + 1, 4).Value?.ToString();
            if (!Guid.TryParse(formIdValue, out var formId)) continue;
            if (string.IsNullOrWhiteSpace(tabId) || formId == Guid.Empty || !Guid.TryParse(tabId, out var tabGuid)) continue;

            var form = forms.FirstOrDefault(f => f.Id == formId);
            if (form == null)
            {
                form = service.Retrieve("systemform", formId, new ColumnSet("formxml"));
                forms.Add(form);
            }

            var formXml = form.GetAttributeValue<string>("formxml");
            var docXml = new XmlDocument();
            docXml.LoadXml(formXml ?? string.Empty);
            var root = docXml.DocumentElement;
            if (root is null)
            {
                continue;
            }

            var tabNode = root.SelectSingleNode(
                string.Format("tabs/tab[translate(@id,'ABCDEFGHIJKLMNOPQRSTUVWXYZ{{}}','abcdefghijklmnopqrstuvwxyz')='{0}']", tabGuid.ToString()));
            if (tabNode != null)
            {
                var columnIndex = 4;
                while (columnIndex < cellsCount)
                {
                    if (sheet.GetCell(rowI + 1, columnIndex + 1).Value != null)
                    {
                        var lcid = sheet.GetCell(1, columnIndex + 1).Value?.ToString();
                        var label = sheet.GetCell(rowI + 1, columnIndex + 1).Value?.ToString() ?? string.Empty;

                        if (!int.TryParse(lcid, out var iLcid))
                        {
                            columnIndex++;
                            continue;
                        }

                        if (!forceUpdate && !IsTranslationUpdated(sheet, rowI, iLcid, label))
                        {
                            columnIndex++;
                            continue;
                        }

                        UpdateXmlNode(tabNode, lcid, label);
                    }

                    columnIndex++;
                }
            }

            form["formxml"] = docXml.OuterXml;
        }
    }

    /// <summary>Applies translated section labels from a TranslationTable into the dashboard XML of each affected dashboard.</summary>
    public void PrepareFormSections(TranslationTable sheet, IOrganizationService service, List<Entity> forms, bool forceUpdate)
    {
        var dimension = sheet.Dimension;
        if (dimension is null)
        {
            return;
        }

        var rowsCount = dimension.Rows;
        var cellsCount = GetLastLcidColumnCount(sheet);

        for (var rowI = 1; rowI < rowsCount; rowI++)
        {
            var sectionId = sheet.GetCell(rowI + 1, 1).Value?.ToString();
            var formIdValue = sheet.GetCell(rowI + 1, 4).Value?.ToString();
            if (!Guid.TryParse(formIdValue, out var formId)) continue;
            if (string.IsNullOrWhiteSpace(sectionId) || formId == Guid.Empty || !Guid.TryParse(sectionId, out var sectionGuid)) continue;

            var form = forms.FirstOrDefault(f => f.Id == formId);
            if (form == null)
            {
                form = service.Retrieve("systemform", formId, new ColumnSet("formxml"));
                forms.Add(form);
            }

            var formXml = form.GetAttributeValue<string>("formxml");
            var docXml = new XmlDocument();
            docXml.LoadXml(formXml ?? string.Empty);
            var root = docXml.DocumentElement;
            if (root is null)
            {
                continue;
            }

            var sectionNode = root.SelectSingleNode(
                string.Format("tabs/tab/columns/column/sections/section[translate(@id,'ABCDEFGHIJKLMNOPQRSTUVWXYZ{{}}','abcdefghijklmnopqrstuvwxyz')='{0}']", sectionGuid.ToString()));
            if (sectionNode != null)
            {
                var columnIndex = 5;
                while (columnIndex < cellsCount)
                {
                    if (sheet.GetCell(rowI + 1, columnIndex + 1).Value != null)
                    {
                        var lcid = sheet.GetCell(1, columnIndex + 1).Value?.ToString();
                        var label = sheet.GetCell(rowI + 1, columnIndex + 1).Value?.ToString() ?? string.Empty;

                        if (!int.TryParse(lcid, out var iLcid))
                        {
                            columnIndex++;
                            continue;
                        }

                        if (!forceUpdate && !IsTranslationUpdated(sheet, rowI, iLcid, label))
                        {
                            columnIndex++;
                            continue;
                        }

                        UpdateXmlNode(sectionNode, lcid, label);
                    }

                    columnIndex++;
                }
            }

            form["formxml"] = docXml.OuterXml;
        }
    }

    /// <summary>Applies translated field labels from a TranslationTable into the dashboard XML of each affected dashboard.</summary>
    public void PrepareFormLabels(TranslationTable sheet, IOrganizationService service, List<Entity> forms, bool forceUpdate)
    {
        var dimension = sheet.Dimension;
        if (dimension is null)
        {
            return;
        }

        var rowsCount = dimension.Rows;
        var cellsCount = GetLastLcidColumnCount(sheet);

        for (var rowI = 1; rowI < rowsCount; rowI++)
        {
            if (HasEmptyRequiredCells(sheet, rowI, 6)) continue;

            var labelId = sheet.GetCell(rowI + 1, 1).Value?.ToString();
            var formIdValue = sheet.GetCell(rowI + 1, 4).Value?.ToString();
            if (!Guid.TryParse(formIdValue, out var formId)) continue;
            if (string.IsNullOrWhiteSpace(labelId) || formId == Guid.Empty || !Guid.TryParse(labelId, out var labelGuid)) continue;

            var form = forms.FirstOrDefault(f => f.Id == formId);
            if (form == null)
            {
                form = service.Retrieve("systemform", formId, new ColumnSet("formxml"));
                forms.Add(form);
            }

            var formXml = form.GetAttributeValue<string>("formxml");
            var docXml = new XmlDocument();
            docXml.LoadXml(formXml ?? string.Empty);
            var root = docXml.DocumentElement;
            if (root is null)
            {
                continue;
            }

            var cellNode = root.SelectSingleNode(
                string.Format("tabs/tab/columns/column/sections/section/rows/row/cell[translate(@id,'ABCDEFGHIJKLMNOPQRSTUVWXYZ{{}}','abcdefghijklmnopqrstuvwxyz')='{0}']", labelGuid.ToString()));
            if (cellNode != null)
            {
                var columnIndex = 7;
                while (columnIndex < cellsCount)
                {
                    if (sheet.GetCell(rowI + 1, columnIndex + 1).Value != null)
                    {
                        var lcid = sheet.GetCell(1, columnIndex + 1).Value?.ToString();
                        var label = sheet.GetCell(rowI + 1, columnIndex + 1).Value?.ToString() ?? string.Empty;

                        if (!int.TryParse(lcid, out var iLcid))
                        {
                            columnIndex++;
                            continue;
                        }

                        if (!forceUpdate && !IsTranslationUpdated(sheet, rowI, iLcid, label))
                        {
                            columnIndex++;
                            continue;
                        }

                        UpdateXmlNode(cellNode, lcid, label);
                    }

                    columnIndex++;
                }
            }

            form["formxml"] = docXml.OuterXml;
        }
    }

    // Uses Dimension.Columns (= MaxColumn, 1-based) as the 0-based exclusive upper bound for column loops.
    // This avoids calling GetCell past MaxColumn, which would silently expand the table.
    private static int GetLastLcidColumnCount(TranslationTable sheet)
        => sheet.Dimension?.Columns ?? 0;

    private static bool HasEmptyRequiredCells(TranslationTable sheet, int rowI, int count)
    {
        for (var col = 0; col < count; col++)
        {
            if (string.IsNullOrEmpty(sheet.GetCell(rowI + 1, col + 1).Value?.ToString()))
                return true;
        }
        return false;
    }

    private bool IsTranslationUpdated(TranslationTable sheet, int rowI, int lcid, string label)
    {
        var checksumHeader = $"Checksum_{lcid}";
        var totalCols = sheet.Dimension?.Columns ?? 0;
        for (var c = 1; c <= totalCols; c++)
        {
            var hdr = sheet.GetCell(1, c).Value?.ToString();
            if (string.Equals(hdr, checksumHeader, StringComparison.OrdinalIgnoreCase))
            {
                var storedChecksum = sheet.GetCell(rowI + 1, c).Value?.ToString();
                return !string.Equals(storedChecksum, CalculateChecksum(label), StringComparison.Ordinal);
            }
        }
        return true;
    }

    private void ExtractField(XmlNode cellNode, List<CrmFormLabel> crmFormLabels, Entity form, string tabName,
        string sectionName, int lcid)
    {
        if (cellNode.Attributes == null) return;

        var cellIdAttr = cellNode.Attributes["id"];
        if (cellIdAttr == null) return;
        if (!Guid.TryParse(cellIdAttr.Value, out var cellGuid)) return;
        if (cellNode.ChildNodes.Count == 0) return;

        var controlNode = cellNode.SelectSingleNode("control");
        if (controlNode?.Attributes == null) return;

        var controlId = controlNode.Attributes["id"]?.Value;
        if (string.IsNullOrWhiteSpace(controlId)) return;

        var crmFormField = crmFormLabels.FirstOrDefault(
            f => f.Id == cellGuid && f.FormUniqueId == form.GetAttributeValue<Guid>("formidunique"));
        if (crmFormField == null)
        {
            crmFormField = new CrmFormLabel
            {
                Id = cellGuid,
                Form = form.GetAttributeValue<string>("name") ?? string.Empty,
                FormUniqueId = form.GetAttributeValue<Guid>("formidunique"),
                FormId = form.GetAttributeValue<Guid>("formid"),
                Tab = tabName,
                Section = sectionName,
                Attribute = controlId,
                Names = new Dictionary<int, string>()
            };
            crmFormLabels.Add(crmFormField);
        }

        if (crmFormField.Names.ContainsKey(lcid)) return;

        var labelNode = cellNode.SelectSingleNode("labels/label[@languagecode='" + lcid + "']");
        crmFormField.Names.Add(lcid, labelNode?.Attributes?["description"]?.Value ?? string.Empty);
    }

    private string ExtractSection(XmlNode sectionNode, int lcid, List<CrmFormSection> crmFormSections, Entity form,
        string tabName)
    {
        var sectionIdAttr = sectionNode.Attributes?["id"];
        if (sectionIdAttr == null) return string.Empty;
        var sectionId = sectionIdAttr.Value;

        var sectionLabelNode = sectionNode.SelectSingleNode("labels/label[@languagecode='" + lcid + "']");
        if (sectionLabelNode?.Attributes == null) return string.Empty;

        var sectionName = sectionLabelNode.Attributes["description"]?.Value ?? string.Empty;
        if (!Guid.TryParse(sectionId, out var sectionGuid)) return string.Empty;

        var crmFormSection = crmFormSections.FirstOrDefault(
            f => f.Id == sectionGuid && f.FormUniqueId == form.GetAttributeValue<Guid>("formidunique"));
        if (crmFormSection == null)
        {
            crmFormSection = new CrmFormSection
            {
                Id = sectionGuid,
                FormUniqueId = form.GetAttributeValue<Guid>("formidunique"),
                FormId = form.GetAttributeValue<Guid>("formid"),
                Form = form.GetAttributeValue<string>("name") ?? string.Empty,
                Tab = tabName,
                Names = new Dictionary<int, string>()
            };
            crmFormSections.Add(crmFormSection);
        }

        if (crmFormSection.Names.ContainsKey(lcid)) return sectionName;
        crmFormSection.Names.Add(lcid, sectionName);
        return sectionName;
    }

    private string ExtractTabName(XmlNode tabNode, int lcid, List<CrmFormTab> crmFormTabs, Entity form)
    {
        var tabIdAttr = tabNode.Attributes?["id"];
        if (tabIdAttr == null) return string.Empty;
        var tabId = tabIdAttr.Value;

        var tabLabelNode = tabNode.SelectSingleNode("labels/label[@languagecode='" + lcid + "']");
        if (tabLabelNode?.Attributes == null) return string.Empty;

        var tabName = tabLabelNode.Attributes["description"]?.Value ?? string.Empty;
        if (!Guid.TryParse(tabId, out var tabGuid)) return string.Empty;

        var crmFormTab = crmFormTabs.FirstOrDefault(
            f => f.Id == tabGuid && f.FormUniqueId == form.GetAttributeValue<Guid>("formidunique"));
        if (crmFormTab == null)
        {
            crmFormTab = new CrmFormTab
            {
                Id = tabGuid,
                FormUniqueId = form.GetAttributeValue<Guid>("formidunique"),
                FormId = form.GetAttributeValue<Guid>("formid"),
                Form = form.GetAttributeValue<string>("name") ?? string.Empty,
                Names = new Dictionary<int, string>()
            };
            crmFormTabs.Add(crmFormTab);
        }

        if (crmFormTab.Names.ContainsKey(lcid)) return tabName;
        crmFormTab.Names.Add(lcid, tabName);
        return tabName;
    }

    private static Entity GetCurrentUserSettings(IOrganizationService service)
    {
        var qe = new QueryExpression("usersettings")
        {
            ColumnSet = new ColumnSet("uilanguageid", "localeid")
        };
        qe.Criteria.AddCondition("systemuserid", ConditionOperator.EqualUserId);
        return service.RetrieveMultiple(qe).Entities.FirstOrDefault() ?? new Entity("usersettings");
    }

    private static List<Entity> RetrieveDashboardList(List<Guid> ids, IOrganizationService service, bool enableManaged)
    {
        var qe = new QueryExpression("systemform")
        {
            ColumnSet = new ColumnSet(true),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("type", ConditionOperator.Equal, 0)
                }
            }
        };

        if (!enableManaged)
        {
            qe.Criteria.AddCondition("ismanaged", ConditionOperator.Equal, false);
        }

        if (ids.Count != 0)
        {
            var fe = qe.Criteria.AddFilter(LogicalOperator.Or);
            foreach (var id in ids)
            {
                fe.AddCondition("formid", ConditionOperator.Equal, id);
            }
        }

        return service.RetrieveMultiple(qe).Entities.ToList();
    }

    private static void UpdateXmlNode(XmlNode node, string? lcid, string? description)
    {
        var safeLcid = lcid ?? string.Empty;
        var safeDescription = description ?? string.Empty;
        var ownerDocument = node.OwnerDocument;
        if (ownerDocument == null) return;

        var labelsNode = node.SelectSingleNode("labels")
            ?? node.AppendChild(ownerDocument.CreateElement("labels"))!;

        var labelNode = labelsNode.SelectSingleNode(string.Format("label[@languagecode='{0}']", safeLcid));
        if (labelNode == null)
        {
            labelNode = ownerDocument.CreateElement("label");
            labelsNode.AppendChild(labelNode);

            var languageAttr = ownerDocument.CreateAttribute("languagecode");
            languageAttr.Value = safeLcid;
            labelNode.Attributes!.Append(languageAttr);
            labelNode.Attributes.Append(ownerDocument.CreateAttribute("description"));
        }

        var descriptionAttr = labelNode.Attributes?["description"];
        if (descriptionAttr == null)
        {
            descriptionAttr = ownerDocument.CreateAttribute("description");
            labelNode.Attributes!.Append(descriptionAttr);
        }

        descriptionAttr.Value = safeDescription;
    }

    // MetadataJson keys must match the column order used by ImportFormName and PrepareForm* methods.
    // Dashboards: col 0=Form Unique Id, 1=Form Id, 2=Type, 3+=LCIDs
    private static string SerializeDashboardsMetadata(string formUniqueId, string formId, string type)
        => JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Form Unique Id"] = formUniqueId,
            ["Form Id"] = formId,
            ["Type"] = type
        });

    // Dashboards Tabs: col 0=Tab Id, 1=Form Name, 2=Form Unique Id, 3=Form Id, 4+=LCIDs
    private static string SerializeDashboardTabsMetadata(string tabId, string formName, string formUniqueId, string formId)
        => JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Tab Id"] = tabId,
            ["Form Name"] = formName,
            ["Form Unique Id"] = formUniqueId,
            ["Form Id"] = formId
        });

    // Dashboards Sections: col 0=Section Id, 1=Form Name, 2=Form Unique Id, 3=Form Id, 4=Tab Name, 5+=LCIDs
    private static string SerializeDashboardSectionsMetadata(string sectionId, string formName, string formUniqueId, string formId, string tabName)
        => JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Section Id"] = sectionId,
            ["Form Name"] = formName,
            ["Form Unique Id"] = formUniqueId,
            ["Form Id"] = formId,
            ["Tab Name"] = tabName
        });

    // Dashboards Fields: col 0=Label Id, 1=Form Name, 2=Form Unique Id, 3=Form Id, 4=Tab Name, 5=Section Name, 6=Attribute, 7+=LCIDs
    private static string SerializeDashboardFieldsMetadata(string labelId, string formName, string formUniqueId, string formId, string tabName, string sectionName, string attribute)
        => JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Label Id"] = labelId,
            ["Form Name"] = formName,
            ["Form Unique Id"] = formUniqueId,
            ["Form Id"] = formId,
            ["Tab Name"] = tabName,
            ["Section Name"] = sectionName,
            ["Attribute"] = attribute
        });
}
