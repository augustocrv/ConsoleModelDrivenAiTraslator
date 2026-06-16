namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models;

/// <summary>
/// Root configuration for custom translation behaviors.
/// </summary>
/// <summary>Class description.</summary>
public sealed class ConsoleModelDrivenAiTraslatorConfig
{
    /// <summary>
    /// Optional translation context/instructions appended to AI prompts.
    /// </summary>
    public string TranslationContext { get; set; } = string.Empty;

    public FieldTranslationConfig FieldTranslation { get; set; } = new();

    public ViewTranslationConfig ViewTranslation { get; set; } = new();
}

/// <summary>
/// Configures constant/template translations for Dataverse field display names.
/// </summary>
/// <summary>Class description.</summary>
public sealed class FieldTranslationConfig
{
    /// <summary>
    /// Per-target-language map: logical field name -> translated display name.
    /// Supports optional '*' key as template (may use {logicalName}).
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> LogicalNameTranslations { get; set; } = new();
}

/// <summary>
/// Configures constant and modular translations for Dataverse views.
/// </summary>
/// <summary>Class description.</summary>
public sealed class ViewTranslationConfig
{
    /// <summary>
    /// Maps exported view type text to a logical rule key (for example: "Lookup view" -> "lookup").
    /// </summary>
    public Dictionary<string, string> ViewTypeRuleMap { get; set; } = new();

    /// <summary>
    /// Source-language patterns used to infer a rule key from the original text.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> SourcePatterns { get; set; } = new();

    /// <summary>
    /// Per-target-language rule templates.
    /// </summary>
    public Dictionary<string, Dictionary<string, ViewTranslationTemplateConfig>> Templates { get; set; } = new();

    /// <summary>
    /// Optional exact per-language source-to-target constants (highest priority).
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> ExactTranslations { get; set; } = new();

    /// <summary>
    /// Optional per-language entity gender map (logical name -> masculine/feminine/neutral).
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> EntityGenderByLanguage { get; set; } = new();
}

/// <summary>
/// Defines a translation template with optional gender variants.
/// </summary>
/// <summary>Class description.</summary>
public sealed class ViewTranslationTemplateConfig
{
    public string? Default { get; set; }

    public Dictionary<string, string> Gender { get; set; } = new();
}

