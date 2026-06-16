namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

internal sealed class FormRecordExporter : BaseRecordExporter
{
    private const string DatasetForms = "Forms";
    private const string DatasetTabs = "Forms Tabs";
    private const string DatasetSections = "Forms Sections";
    private const string DatasetFields = "Forms Fields";

    // Accumulated during Prepare* calls, consumed in ImportFormsContent
    private readonly List<int> languagesToProcess = new();

    /// <summary>Exports form names/descriptions and form content (tabs, sections, fields) as TranslationRecord objects.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<TranslationRecord>> Export(
        List<EntityMetadata> entities,
        int sourceLcid,
        IReadOnlyList<int> allLcids,
        IOrganizationService service,
        ExportSettings settings)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(allLcids);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(settings);

        var sourceLcidStr = sourceLcid.ToString(CultureInfo.InvariantCulture);

        var userSetting = GetCurrentUserSettings(service);
        var userSettingLcid = userSetting.GetAttributeValue<int>("uilanguageid");
        if (userSettingLcid == 0) userSettingLcid = allLcids.Count > 0 ? allLcids[0] : sourceLcid;
        var currentSetting = userSettingLcid;

        var crmForms = new List<CrmForm>();
        var crmFormTabs = new List<CrmFormTab>();
        var crmFormSections = new List<CrmFormSection>();
        var crmFormLabels = new List<CrmFormLabel>();

        // Collect form XML-based content (tabs, sections, fields) for each language
        foreach (var lcid in allLcids)
        {
            if (currentSetting != lcid)
            {
                userSetting["localeid"] = lcid;
                userSetting["uilanguageid"] = lcid;
                userSetting["helplanguageid"] = lcid;
                service.Update(userSetting);
                currentSetting = lcid;
                Thread.Sleep(2000);
            }

            foreach (var entity in entities.OrderBy(e => e.LogicalName))
            {
                if (!entity.MetadataId.HasValue) continue;

                var forms = RetrieveEntityFormList(entity.LogicalName, service, settings.EnableManaged);
                foreach (var form in forms)
                {
                    var sFormXml = form.GetAttributeValue<string>("formxml");
                    var formXml = new XmlDocument();
                    formXml.LoadXml(sFormXml ?? string.Empty);

                    var headerCellNodes = formXml.DocumentElement?.SelectNodes("header/rows/row/cell");
                    if (headerCellNodes != null)
                    {
                        foreach (XmlNode cellNode in headerCellNodes)
                        {
                            ExtractField(cellNode, crmFormLabels, form, null, null, entity, lcid, settings.EnableManaged);
                        }
                    }

                    var tabNodes = formXml.SelectNodes("//tab");
                    if (tabNodes == null) continue;

                    foreach (XmlNode tabNode in tabNodes)
                    {
                        var tabName = ExtractTabName(tabNode, lcid, crmFormTabs, form, entity);
                        var sectionNodes = tabNode.SelectNodes("columns/column/sections/section");
                        if (sectionNodes == null) continue;

                        foreach (XmlNode sectionNode in sectionNodes)
                        {
                            var sectionName = ExtractSection(sectionNode, lcid, crmFormSections, form, tabName, entity);
                            var labelNodes = sectionNode.SelectNodes("rows/row/cell");
                            if (labelNodes == null) continue;

                            foreach (XmlNode labelNode in labelNodes)
                            {
                                ExtractField(labelNode, crmFormLabels, form, tabName, sectionName, entity, lcid, settings.EnableManaged);
                            }
                        }
                    }

                    var footerCellNodes = formXml.DocumentElement?.SelectNodes("footer/rows/row/cell");
                    if (footerCellNodes != null)
                    {
                        foreach (XmlNode cellNode in footerCellNodes)
                        {
                            ExtractField(cellNode, crmFormLabels, form, null, null, entity, lcid, settings.EnableManaged);
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

        // Collect localized form names/descriptions via RetrieveLocLabelsRequest
        foreach (var entity in entities.OrderBy(e => e.LogicalName))
        {
            if (!entity.MetadataId.HasValue) continue;

            var forms = RetrieveEntityFormList(entity.LogicalName, service, settings.EnableManaged);
            foreach (var form in forms)
            {
                var crmForm = crmForms.FirstOrDefault(f => f.FormUniqueId == form.GetAttributeValue<Guid>("formidunique"));
                if (crmForm == null)
                {
                    crmForm = new CrmForm
                    {
                        FormUniqueId = form.GetAttributeValue<Guid>("formidunique"),
                        Id = form.GetAttributeValue<Guid>("formid"),
                        Entity = entity.LogicalName,
                        Names = new Dictionary<int, string>(),
                        Descriptions = new Dictionary<int, string>(),
                        Type = form.FormattedValues.TryGetValue("type", out var formType) ? formType : string.Empty
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
        }

        // Build TranslationRecord collections
        var formsRecords = new List<TranslationRecord>();
        var tabsRecords = new List<TranslationRecord>();
        var sectionsRecords = new List<TranslationRecord>();
        var fieldsRecords = new List<TranslationRecord>();

        var formRowNumber = 2;
        foreach (var crmForm in crmForms.OrderBy(f => f.Entity).ThenBy(f => f.FormUniqueId.ToString()))
        {
            var formUniqueId = crmForm.FormUniqueId.ToString("B");
            var formId = crmForm.Id.ToString("B");

            if (settings.ExportNames)
            {
                var sourceText = crmForm.Names.TryGetValue(sourceLcid, out var sn) ? sn : string.Empty;
                var rowNumber = formRowNumber++;
                var metadataJson = SerializeFormsMetadata(formUniqueId, formId, crmForm.Entity, crmForm.Type, "Name");
                foreach (var targetLcid in allLcids)
                {
                    var targetLcidStr = targetLcid.ToString(CultureInfo.InvariantCulture);
                    var targetText = crmForm.Names.TryGetValue(targetLcid, out var tn) ? tn : string.Empty;
                    formsRecords.Add(new TranslationRecord
                    {
                        Dataset = DatasetForms,
                        RecordKey = $"{DatasetForms}|{formUniqueId}|Name|{targetLcidStr}",
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
                var metadataJson = SerializeFormsMetadata(formUniqueId, formId, crmForm.Entity, crmForm.Type, "Description");
                foreach (var targetLcid in allLcids)
                {
                    var targetLcidStr = targetLcid.ToString(CultureInfo.InvariantCulture);
                    var targetText = crmForm.Descriptions.TryGetValue(targetLcid, out var td) ? td : string.Empty;
                    formsRecords.Add(new TranslationRecord
                    {
                        Dataset = DatasetForms,
                        RecordKey = $"{DatasetForms}|{formUniqueId}|Description|{targetLcidStr}",
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
            var metadataJson = SerializeTabsMetadata(tabId, crmFormTab.Entity, crmFormTab.Form, formUniqueId, formId);
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
            var metadataJson = SerializeSectionsMetadata(sectionId, crmFormSection.Entity, crmFormSection.Form, formUniqueId, formId, crmFormSection.Tab);
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
            var metadataJson = SerializeFieldsMetadata(labelId, crmFormLabel.Entity, crmFormLabel.Form, formUniqueId, formId, crmFormLabel.Tab, crmFormLabel.Section, crmFormLabel.Attribute, crmFormLabel.AttributeDisplayName);
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
        if (formsRecords.Count > 0) result[DatasetForms] = formsRecords;
        if (tabsRecords.Count > 0) result[DatasetTabs] = tabsRecords;
        if (sectionsRecords.Count > 0) result[DatasetSections] = sectionsRecords;
        if (fieldsRecords.Count > 0) result[DatasetFields] = fieldsRecords;
        return result;
    }

    /// <summary>Imports localized form names and descriptions from a TranslationTable.</summary>
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

            var typeValue = sheet.GetCell(rowI + 1, 5).Value?.ToString();
            var attributeName = typeValue == "Name" ? "name" : "description";

            var locLabel = ((RetrieveLocLabelsResponse)service.Execute(new RetrieveLocLabelsRequest
            {
                EntityMoniker = new EntityReference("systemform", currentFormId),
                AttributeName = attributeName
            })).Label;

            if (locLabel?.LocalizedLabels == null)
            {
                continue;
            }

            var labels = locLabel.LocalizedLabels.ToList();
            var request = new SetLocLabelsRequest
            {
                RequestId = Guid.NewGuid(),
                EntityMoniker = new EntityReference("systemform", currentFormId),
                AttributeName = attributeName,
                Labels = locLabel.LocalizedLabels.ToArray()
            };

            var columnIndex = 5;
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

                    var translatedLabel = labels.FirstOrDefault(x => x.LanguageCode == lcid);
                    if (translatedLabel == null)
                    {
                        translatedLabel = new LocalizedLabel(label, lcid);
                        labels.Add(translatedLabel);
                    }
                    else
                    {
                        translatedLabel.Label = label;
                    }

                    if (request.RequestId.HasValue && !needUpdate.Contains(request.RequestId.Value))
                    {
                        needUpdate.Add(request.RequestId.Value);
                    }
                }

                columnIndex++;
            }

            request.Labels = labels.ToArray();
            requests.Add(request);
        }

        var setting = GetCurrentUserSettings(service);
        var userSettingLcid = setting.GetAttributeValue<int>("uilanguageid");
        var currentSetting = userSettingLcid;

        var orgLcid = GetCurrentOrgBaseLanguage(service);
        if (currentSetting != orgLcid)
        {
            setting["localeid"] = orgLcid;
            setting["uilanguageid"] = orgLcid;
            setting["helplanguageid"] = orgLcid;
            service.Update(setting);
            currentSetting = orgLcid;
            Thread.Sleep(2000);
        }

        var arg = new TranslationProgressEventArgs { SheetName = sheet.Name };
        var requestsNeedingUpdate = requests.Where(r => r.RequestId.HasValue && needUpdate.Contains(r.RequestId.Value)).ToList();
        foreach (var request in requestsNeedingUpdate)
        {
            AddRequest(request);
            ExecuteMultiple(service, arg, requestsNeedingUpdate.Count);
        }
        ExecuteMultiple(service, arg, requestsNeedingUpdate.Count, true);

        if (currentSetting != userSettingLcid)
        {
            setting["localeid"] = userSettingLcid;
            setting["uilanguageid"] = userSettingLcid;
            setting["helplanguageid"] = userSettingLcid;
            service.Update(setting);
            Thread.Sleep(2000);
        }
    }

    /// <summary>Pushes updated form XML content back to Dataverse for each language that was modified.</summary>
    public void ImportFormsContent(IOrganizationService service, List<Entity> forms)
    {
        var setting = GetCurrentUserSettings(service);
        var userSettingLcid = setting.GetAttributeValue<int>("uilanguageid");
        var currentSetting = userSettingLcid;

        foreach (var lcid in languagesToProcess)
        {
            if (currentSetting != lcid)
            {
                setting["localeid"] = lcid;
                setting["uilanguageid"] = lcid;
                setting["helplanguageid"] = lcid;
                service.Update(setting);
                currentSetting = lcid;
            }

            var arg = new TranslationProgressEventArgs();
            foreach (var form in forms)
            {
                AddRequest(new UpdateRequest { Target = form });
                ExecuteMultiple(service, arg, forms.Count);
            }
            ExecuteMultiple(service, arg, forms.Count, true);
        }

        if (currentSetting != userSettingLcid)
        {
            setting["localeid"] = userSettingLcid;
            setting["uilanguageid"] = userSettingLcid;
            setting["helplanguageid"] = userSettingLcid;
            service.Update(setting);
        }
    }

    /// <summary>Applies translated tab labels from a TranslationTable into the form XML of each affected form.</summary>
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
            if (HasEmptyRequiredCells(sheet, rowI, 4)) continue;

            var tabId = sheet.GetCell(rowI + 1, 1).Value?.ToString();
            var formIdValue = sheet.GetCell(rowI + 1, 5).Value?.ToString();
            if (!Guid.TryParse(formIdValue, out var formId) || formId == Guid.Empty) continue;
            if (string.IsNullOrWhiteSpace(tabId) || !Guid.TryParse(tabId, out var tabGuid)) continue;

            var form = forms.FirstOrDefault(f => f.Id == formId);
            if (form == null)
            {
                try
                {
                    form = service.Retrieve("systemform", formId, new ColumnSet("formxml"));
                    forms.Add(form);
                }
                catch (Exception ex)
                {
                    AnsiConsole.Console.WriteWarning($"Skipping form: {ex.Message}");
                    continue;
                }
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

                        UpdateXmlNode(tabNode, lcid, label);

                        if (!languagesToProcess.Contains(iLcid)) languagesToProcess.Add(iLcid);
                    }

                    columnIndex++;
                }
            }

            form["formxml"] = docXml.OuterXml;
        }
    }

    /// <summary>Applies translated section labels from a TranslationTable into the form XML of each affected form.</summary>
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
            if (HasEmptyRequiredCells(sheet, rowI, 5)) continue;

            var sectionId = sheet.GetCell(rowI + 1, 1).Value?.ToString();
            var formIdValue = sheet.GetCell(rowI + 1, 5).Value?.ToString();
            if (!Guid.TryParse(formIdValue, out var formId) || formId == Guid.Empty) continue;
            if (string.IsNullOrWhiteSpace(sectionId) || !Guid.TryParse(sectionId, out var sectionGuid)) continue;

            var form = forms.FirstOrDefault(f => f.Id == formId);
            if (form == null)
            {
                try
                {
                    form = service.Retrieve("systemform", formId, new ColumnSet("formxml"));
                    forms.Add(form);
                }
                catch (Exception ex)
                {
                    AnsiConsole.Console.WriteWarning($"Skipping form: {ex.Message}");
                    continue;
                }
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
                var columnIndex = 6;
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

                        if (!languagesToProcess.Contains(iLcid)) languagesToProcess.Add(iLcid);
                    }

                    columnIndex++;
                }
            }

            form["formxml"] = docXml.OuterXml;
        }
    }

    /// <summary>Applies translated field labels from a TranslationTable into the form XML of each affected form.</summary>
    public void PrepareFormLabels(TranslationTable sheet, IOrganizationService service, List<Entity> forms, bool forceUpdate)
    {
        // Col 8 holds "Display name" when present; if it is, LCIDs start at col 9
        var headerEightContent = sheet.GetCell(1, 9).Value?.ToString();
        var hasAttrDisplayNameHeader = headerEightContent == "Display name";
        var startColumnIndex = hasAttrDisplayNameHeader ? 9 : 8;

        var dimension = sheet.Dimension;
        if (dimension is null)
        {
            return;
        }

        var rowsCount = dimension.Rows;
        var cellsCount = GetLastLcidColumnCount(sheet);

        for (var rowI = 1; rowI < rowsCount; rowI++)
        {
            if (HasEmptyRequiredCells(sheet, rowI, startColumnIndex)) continue;

            var labelId = sheet.GetCell(rowI + 1, 1).Value?.ToString();
            var formIdValue = sheet.GetCell(rowI + 1, 5).Value?.ToString();
            if (!Guid.TryParse(formIdValue, out var formId) || formId == Guid.Empty) continue;
            if (string.IsNullOrWhiteSpace(labelId) || !Guid.TryParse(labelId, out var labelGuid)) continue;

            var form = forms.FirstOrDefault(f => f.Id == formId);
            if (form == null)
            {
                try
                {
                    form = service.Retrieve("systemform", formId, new ColumnSet("formxml"));
                    forms.Add(form);
                }
                catch (Exception ex)
                {
                    AnsiConsole.Console.WriteWarning($"Skipping form: {ex.Message}");
                    continue;
                }
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
                string.Format("//cell[translate(@id,'ABCDEFGHIJKLMNOPQRSTUVWXYZ{{}}','abcdefghijklmnopqrstuvwxyz')='{0}']", labelGuid.ToString()));
            if (cellNode != null)
            {
                var columnIndex = startColumnIndex;
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

                        if (!languagesToProcess.Contains(iLcid)) languagesToProcess.Add(iLcid);
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

    private void ExtractField(XmlNode cellNode, List<CrmFormLabel> crmFormLabels, Entity form, string? tabName,
        string? sectionName, EntityMetadata entity, int lcid, bool enableManaged)
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

        var attributeLogicalName = controlId.Split(["header_"], StringSplitOptions.RemoveEmptyEntries).Last();
        var attributeMetadata = entity.Attributes.FirstOrDefault(a => a.LogicalName == attributeLogicalName);
        if (!enableManaged && attributeMetadata != null && IsManagedComponent(attributeMetadata))
        {
            return;
        }

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
                Tab = tabName ?? string.Empty,
                Section = sectionName ?? string.Empty,
                Entity = entity.LogicalName,
                Attribute = controlId,
                AttributeDisplayName = attributeMetadata?.DisplayName?.UserLocalizedLabel?.Label ?? string.Empty,
                Names = new Dictionary<int, string>()
            };
            crmFormLabels.Add(crmFormField);
        }

        if (crmFormField.Names.ContainsKey(lcid)) return;

        var labelNode = cellNode.SelectSingleNode("labels/label[@languagecode='" + lcid + "']");
        var labelDescription = labelNode?.Attributes?["description"];
        crmFormField.Names.Add(lcid, labelDescription?.Value ?? string.Empty);
    }

    private string ExtractSection(XmlNode sectionNode, int lcid, List<CrmFormSection> crmFormSections, Entity form,
        string tabName, EntityMetadata entity)
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
                Entity = entity.LogicalName,
                Names = new Dictionary<int, string>()
            };
            crmFormSections.Add(crmFormSection);
        }

        if (crmFormSection.Names.ContainsKey(lcid)) return sectionName;
        crmFormSection.Names.Add(lcid, sectionName);
        return sectionName;
    }

    private string ExtractTabName(XmlNode tabNode, int lcid, List<CrmFormTab> crmFormTabs, Entity form,
        EntityMetadata entity)
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
                Entity = entity.LogicalName,
                Names = new Dictionary<int, string>()
            };
            crmFormTabs.Add(crmFormTab);
        }

        if (crmFormTab.Names.ContainsKey(lcid)) return tabName;
        crmFormTab.Names.Add(lcid, tabName);
        return tabName;
    }

    private static int GetCurrentOrgBaseLanguage(IOrganizationService service)
    {
        var qe = new QueryExpression("organization") { ColumnSet = new ColumnSet("languagecode") };
        var result = service.RetrieveMultiple(qe);
        return result.Entities.FirstOrDefault()?.GetAttributeValue<int>("languagecode") ?? 0;
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

    private static IEnumerable<Entity> RetrieveEntityFormList(string logicalName, IOrganizationService service, bool enableManaged)
    {
        var qe = new QueryExpression("systemform")
        {
            ColumnSet = new ColumnSet(true),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("objecttypecode", ConditionOperator.Equal, logicalName),
                    new ConditionExpression("type", ConditionOperator.In, new[] { 2, 6, 7 })
                }
            }
        };

        if (!enableManaged)
        {
            qe.Criteria.AddCondition("ismanaged", ConditionOperator.Equal, false);
        }

        return service.RetrieveMultiple(qe).Entities;
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

    // MetadataJson keys must match the column order used by PrepareForm* methods.
    // Forms: col 0=Form Unique Id, 1=Form Id, 2=Entity Logical Name, 3=Form Type, 4=Type, 5+=LCIDs
    private static string SerializeFormsMetadata(string formUniqueId, string formId, string entityLogicalName, string formType, string type)
        => JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Form Unique Id"] = formUniqueId,
            ["Form Id"] = formId,
            ["Entity Logical Name"] = entityLogicalName,
            ["Form Type"] = formType,
            ["Type"] = type
        });

    // Forms Tabs: col 0=Tab Id, 1=Entity Logical Name, 2=Form Name, 3=Form Unique Id, 4=Form Id, 5+=LCIDs
    private static string SerializeTabsMetadata(string tabId, string entityLogicalName, string formName, string formUniqueId, string formId)
        => JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Tab Id"] = tabId,
            ["Entity Logical Name"] = entityLogicalName,
            ["Form Name"] = formName,
            ["Form Unique Id"] = formUniqueId,
            ["Form Id"] = formId
        });

    // Forms Sections: col 0=Section Id, 1=Entity Logical Name, 2=Form Name, 3=Form Unique Id, 4=Form Id, 5=Tab Name, 6+=LCIDs
    private static string SerializeSectionsMetadata(string sectionId, string entityLogicalName, string formName, string formUniqueId, string formId, string tabName)
        => JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Section Id"] = sectionId,
            ["Entity Logical Name"] = entityLogicalName,
            ["Form Name"] = formName,
            ["Form Unique Id"] = formUniqueId,
            ["Form Id"] = formId,
            ["Tab Name"] = tabName
        });

    // Forms Fields: col 0=Label Id, 1=Entity Logical Name, 2=Form Name, 3=Form Unique Id, 4=Form Id, 5=Tab Name, 6=Section Name, 7=Attribute, 8=Display name, 9+=LCIDs
    private static string SerializeFieldsMetadata(string labelId, string entityLogicalName, string formName, string formUniqueId, string formId, string tabName, string sectionName, string attribute, string displayName)
        => JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Label Id"] = labelId,
            ["Entity Logical Name"] = entityLogicalName,
            ["Form Name"] = formName,
            ["Form Unique Id"] = formUniqueId,
            ["Form Id"] = formId,
            ["Tab Name"] = tabName,
            ["Section Name"] = sectionName,
            ["Attribute"] = attribute,
            ["Display name"] = displayName
        });
}
