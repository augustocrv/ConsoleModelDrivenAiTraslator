namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services;

internal sealed class ConsoleModelDrivenAiTraslatorConfigJsonService
{
    public ConsoleModelDrivenAiTraslatorConfig ParseConsoleModelDrivenAiTraslatorConfig(string json)
    {
        var config = new ConsoleModelDrivenAiTraslatorConfig();

        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        if (document.RootElement.TryGetProperty("translationContext", out var translationContextElement) &&
            translationContextElement.ValueKind == JsonValueKind.String)
        {
            config.TranslationContext = translationContextElement.GetString() ?? string.Empty;
        }

        if (!document.RootElement.TryGetProperty("viewTranslation", out var viewTranslationElement))
        {
            if (document.RootElement.TryGetProperty("fieldTranslation", out var fieldTranslationElement))
            {
                config.FieldTranslation = new FieldTranslationConfig
                {
                    LogicalNameTranslations = ResolveAliasedSection(
                        ReadRawAliasedSection(fieldTranslationElement, "logicalNameTranslations"),
                        DeserializeStringDictionary,
                        CloneStringDictionary)
                };
            }

            return config;
        }

        var fieldTranslation = new FieldTranslationConfig();
        if (document.RootElement.TryGetProperty("fieldTranslation", out var fieldTranslationSection))
        {
            fieldTranslation = new FieldTranslationConfig
            {
                LogicalNameTranslations = ResolveAliasedSection(
                    ReadRawAliasedSection(fieldTranslationSection, "logicalNameTranslations"),
                    DeserializeStringDictionary,
                    CloneStringDictionary)
            };
        }

        var viewTranslation = new ViewTranslationConfig
        {
            ViewTypeRuleMap = ReadDictionaryOrDefault(viewTranslationElement, "viewTypeRuleMap"),
            SourcePatterns = ReadNestedDictionaryOrDefault(viewTranslationElement, "sourcePatterns"),
            Templates = ResolveAliasedSection(
                ReadRawAliasedSection(viewTranslationElement, "templates"),
                DeserializeTemplateDictionary,
                CloneTemplateDictionary),
            ExactTranslations = ResolveAliasedSection(
                ReadRawAliasedSection(viewTranslationElement, "exactTranslations"),
                DeserializeStringDictionary,
                CloneStringDictionary),
            EntityGenderByLanguage = ResolveAliasedSection(
                ReadRawAliasedSection(viewTranslationElement, "entityGenderByLanguage"),
                DeserializeStringDictionary,
                CloneStringDictionary)
        };

        config.FieldTranslation = fieldTranslation;
        config.ViewTranslation = viewTranslation;
        return config;
    }

    public string SerializeConsoleModelDrivenAiTraslatorConfig(ConsoleModelDrivenAiTraslatorConfig config)
    {
        var view = config.ViewTranslation ?? new ViewTranslationConfig();

        var payload = new
        {
            translationContext = config.TranslationContext,
            fieldTranslation = new
            {
                logicalNameTranslations = BuildLcidSectionArray(config.FieldTranslation.LogicalNameTranslations)
            },
            viewTranslation = new
            {
                viewTypeRuleMap = view.ViewTypeRuleMap,
                sourcePatterns = BuildLcidSectionArray(view.SourcePatterns),
                templates = BuildLcidSectionArray(view.Templates),
                exactTranslations = BuildLcidSectionArray(view.ExactTranslations),
                entityGenderByLanguage = BuildLcidSectionArray(view.EntityGenderByLanguage)
            }
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    public ConsoleModelDrivenAiTraslatorConfig BuildConfigForTargetLanguages(ConsoleModelDrivenAiTraslatorConfig sourceConfig, HashSet<string> targetLanguages)
    {
        if (targetLanguages.Count == 0)
        {
            return sourceConfig;
        }

        var viewSource = sourceConfig.ViewTranslation ?? new ViewTranslationConfig();
        var fieldSource = sourceConfig.FieldTranslation ?? new FieldTranslationConfig();

        return new ConsoleModelDrivenAiTraslatorConfig
        {
            TranslationContext = sourceConfig.TranslationContext,
            FieldTranslation = new FieldTranslationConfig
            {
                LogicalNameTranslations = FilterByTargetLanguage(fieldSource.LogicalNameTranslations, targetLanguages)
            },
            ViewTranslation = new ViewTranslationConfig
            {
                ViewTypeRuleMap = new Dictionary<string, string>(viewSource.ViewTypeRuleMap, StringComparer.OrdinalIgnoreCase),
                SourcePatterns = FilterByTargetLanguage(viewSource.SourcePatterns, targetLanguages),
                Templates = FilterByTargetLanguage(viewSource.Templates, targetLanguages),
                EntityGenderByLanguage = FilterByTargetLanguage(viewSource.EntityGenderByLanguage, targetLanguages),
                ExactTranslations = FilterByTargetLanguage(viewSource.ExactTranslations, targetLanguages)
            }
        };
    }

    private static Dictionary<string, Dictionary<string, TValue>> FilterByTargetLanguage<TValue>(
        Dictionary<string, Dictionary<string, TValue>> source,
        HashSet<string> targetLanguages)
    {
        var result = new Dictionary<string, Dictionary<string, TValue>>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in source)
        {
            if (!targetLanguages.Contains(pair.Key))
            {
                continue;
            }

            result[pair.Key] = pair.Value;
        }

        return result;
    }

    private static Dictionary<string, string> ReadDictionaryOrDefault(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(property.GetRawText(), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });

        return parsed is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, Dictionary<string, string>> ReadNestedDictionaryOrDefault(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        }

        var parsed = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(property.GetRawText(), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });

        if (parsed is null)
        {
            return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        }

        return parsed.ToDictionary(
            kvp => kvp.Key,
            kvp => new Dictionary<string, string>(kvp.Value, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, RawAliasedValue> ReadRawAliasedSection(JsonElement parent, string propertyName)
    {
        var result = new Dictionary<string, RawAliasedValue>(StringComparer.OrdinalIgnoreCase);
        if (!parent.TryGetProperty(propertyName, out var section))
        {
            return result;
        }

        if (section.ValueKind == JsonValueKind.Object)
        {
            foreach (var item in section.EnumerateObject())
            {
                if (item.Value.ValueKind == JsonValueKind.String)
                {
                    result[item.Name] = new RawAliasedValue
                    {
                        Alias = item.Value.GetString() ?? string.Empty
                    };
                }
                else if (item.Value.ValueKind == JsonValueKind.Object)
                {
                    result[item.Name] = new RawAliasedValue
                    {
                        RawJson = item.Value.GetRawText()
                    };
                }
            }

            return result;
        }

        if (section.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in section.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!TryReadLcidKey(item, out var lcidKey))
            {
                continue;
            }

            if (item.TryGetProperty("alias", out var aliasElement))
            {
                var alias = aliasElement.ValueKind switch
                {
                    JsonValueKind.Number => aliasElement.GetInt32().ToString(CultureInfo.InvariantCulture),
                    JsonValueKind.String => aliasElement.GetString() ?? string.Empty,
                    _ => string.Empty
                };

                if (!string.IsNullOrWhiteSpace(alias))
                {
                    result[lcidKey] = new RawAliasedValue { Alias = alias };
                    continue;
                }
            }

            if (item.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.Object)
            {
                result[lcidKey] = new RawAliasedValue
                {
                    RawJson = valueElement.GetRawText()
                };
            }
        }

        return result;
    }

    private static Dictionary<string, TSection> ResolveAliasedSection<TSection>(
        Dictionary<string, RawAliasedValue> rawSection,
        Func<string, TSection> deserialize,
        Func<TSection, TSection> clone)
        where TSection : notnull
    {
        var resolved = new Dictionary<string, TSection>(StringComparer.OrdinalIgnoreCase);
        var inProgress = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        TSection Resolve(string key)
        {
            if (resolved.TryGetValue(key, out var cached))
            {
                return cached;
            }

            if (!rawSection.TryGetValue(key, out var rawValue))
            {
                throw new InvalidOperationException($"Config alias target '{key}' not found.");
            }

            if (!inProgress.Add(key))
            {
                throw new InvalidOperationException($"Circular alias detected for key '{key}'.");
            }

            TSection value;
            if (!string.IsNullOrWhiteSpace(rawValue.Alias))
            {
                value = clone(Resolve(rawValue.Alias));
            }
            else
            {
                value = deserialize(rawValue.RawJson);
            }

            inProgress.Remove(key);
            resolved[key] = value;
            return value;
        }

        foreach (var key in rawSection.Keys)
        {
            Resolve(key);
        }

        return resolved;
    }

    private static bool TryReadLcidKey(JsonElement item, out string lcidKey)
    {
        lcidKey = string.Empty;
        if (!item.TryGetProperty("lcid", out var lcidElement))
        {
            return false;
        }

        if (lcidElement.ValueKind == JsonValueKind.Number)
        {
            lcidKey = lcidElement.GetInt32().ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (lcidElement.ValueKind == JsonValueKind.String)
        {
            var raw = lcidElement.GetString();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                lcidKey = raw;
                return true;
            }
        }

        return false;
    }

    private static object[] BuildLcidSectionArray<TValue>(Dictionary<string, Dictionary<string, TValue>> section)
        where TValue : notnull
    {
        return section
            .Select(pair => new
            {
                lcid = int.TryParse(pair.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lcid)
                    ? lcid
                    : 0,
                value = pair.Value
            })
            .OrderBy(item => item.lcid)
            .ToArray<object>();
    }

    private static Dictionary<string, ViewTranslationTemplateConfig> DeserializeTemplateDictionary(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return new Dictionary<string, ViewTranslationTemplateConfig>(StringComparer.OrdinalIgnoreCase);
        }

        var parsed = JsonSerializer.Deserialize<Dictionary<string, ViewTranslationTemplateConfig>>(rawJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        }) ?? new Dictionary<string, ViewTranslationTemplateConfig>();

        return parsed.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> DeserializeStringDictionary(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(rawJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        }) ?? new Dictionary<string, string>();

        return new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, ViewTranslationTemplateConfig> CloneTemplateDictionary(Dictionary<string, ViewTranslationTemplateConfig> source)
    {
        var copy = new Dictionary<string, ViewTranslationTemplateConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in source)
        {
            copy[item.Key] = new ViewTranslationTemplateConfig
            {
                Default = item.Value.Default,
                Gender = new Dictionary<string, string>(item.Value.Gender, StringComparer.OrdinalIgnoreCase)
            };
        }

        return copy;
    }

    private static Dictionary<string, string> CloneStringDictionary(Dictionary<string, string> source)
    {
        return new Dictionary<string, string>(source, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class RawAliasedValue
    {
        public string Alias { get; set; } = string.Empty;

        public string RawJson { get; set; } = string.Empty;
    }
}
