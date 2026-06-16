[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DeploymentEndpoint,

    [Parameter(Mandatory = $true)]
    [string]$ApiKey,

    [string]$ConfigPath = "src/AugustoCRV.Tools.ConsoleModelDrivenAiTraslator/Resources/DefaultConsoleModelDrivenAiTraslatorConfig.json",
    [string]$WindowsLcidPath = "src/AugustoCRV.Tools.ConsoleModelDrivenAiTraslator/Resources/WindowsLcid.json",
    [int]$BatchSize = 20,
    [int]$PauseMilliseconds = 0,
    [string[]]$IncludeLcid = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Copy-Hashtable {
    param([hashtable]$Value)

    if ($null -eq $Value) {
        return @{}
    }

    return ($Value | ConvertTo-Json -Depth 40 | ConvertFrom-Json -AsHashtable)
}

function Convert-LcidSectionToMap {
    param([object]$Section)

    $map = @{}
    if ($null -eq $Section) {
        return $map
    }

    if ($Section -is [hashtable]) {
        foreach ($key in $Section.Keys) {
            $map[[string]$key] = $Section[$key]
        }
        return $map
    }

    foreach ($entry in @($Section)) {
        if ($null -eq $entry) {
            continue
        }

        $entryMap = $entry
        if (-not ($entryMap -is [hashtable])) {
            $entryMap = $entryMap | ConvertTo-Json -Depth 20 | ConvertFrom-Json -AsHashtable
        }

        if ($null -eq $entryMap -or -not $entryMap.ContainsKey("lcid")) {
            continue
        }

        $lcidKey = [string]$entryMap["lcid"]
        if ($entryMap.ContainsKey("alias")) {
            $map[$lcidKey] = [string]$entryMap["alias"]
            continue
        }

        if ($entryMap.ContainsKey("value")) {
            $map[$lcidKey] = $entryMap["value"]
        }
    }

    return $map
}

function Convert-LcidMapToArray {
    param([hashtable]$Map)

    $result = @()
    foreach ($key in ($Map.Keys | Sort-Object { [int]$_ })) {
        $value = $Map[$key]
        if ($value -is [string]) {
            $result += [ordered]@{
                lcid = [int]$key
                alias = [int]$value
            }
        }
        else {
            $result += [ordered]@{
                lcid = [int]$key
                value = $value
            }
        }
    }

    return $result
}

function Resolve-TemplateSet {
    param(
        [hashtable]$AllTemplates,
        [string]$Lcid,
        [hashtable]$Visiting
    )

    if (-not $AllTemplates.ContainsKey($Lcid)) {
        throw "Template key '$Lcid' not found."
    }

    $current = $AllTemplates[$Lcid]
    if ($current -is [hashtable]) {
        return $current
    }

    if (-not ($current -is [string])) {
        throw "Template key '$Lcid' must be an object or alias string."
    }

    if ($Visiting.ContainsKey($Lcid)) {
        throw "Circular template alias detected at '$Lcid'."
    }

    $Visiting[$Lcid] = $true
    $resolvedAlias = Resolve-TemplateSet -AllTemplates $AllTemplates -Lcid $current -Visiting $Visiting
    $Visiting.Remove($Lcid) | Out-Null

    $clone = Copy-Hashtable -Value $resolvedAlias
    $AllTemplates[$Lcid] = $clone
    return $clone
}

function Invoke-TranslationBatch {
    param(
        [string]$Endpoint,
        [string]$BearerToken,
        [string]$TargetLanguageName,
        [hashtable]$Items
    )

    $payloadJson = $Items | ConvertTo-Json -Depth 6 -Compress

    $prompt = @"
Translate each value in the JSON object into $TargetLanguageName.
The JSON keys identify view-rule names and must remain unchanged.
Rules:
- Keep the placeholder {entityPlural} exactly as-is.
- Keep output concise and natural for Microsoft Dynamics/Dataverse model-driven app views.
- Return only valid JSON object with the same keys.
Input:
$payloadJson
"@

    $body = @{
        messages = @(
            @{
                role = "user"
                content = $prompt
            }
        )
        temperature = 0.1
    } | ConvertTo-Json -Depth 10

    $headers = @{
        Authorization = "Bearer $BearerToken"
    }

    $response = Invoke-RestMethod -Method Post -Uri $Endpoint -Headers $headers -ContentType "application/json" -Body $body
    $raw = $response.choices[0].message.content

    if ([string]::IsNullOrWhiteSpace($raw)) {
        throw "Empty translation response for language '$TargetLanguageName'."
    }

    try {
        return $raw | ConvertFrom-Json -AsHashtable
    }
    catch {
        throw "Invalid JSON translation response for '$TargetLanguageName': $raw"
    }
}

if (-not (Test-Path $ConfigPath)) {
    throw "Config file not found: $ConfigPath"
}
if (-not (Test-Path $WindowsLcidPath)) {
    throw "Windows LCID file not found: $WindowsLcidPath"
}

$config = Get-Content -Raw $ConfigPath | ConvertFrom-Json -AsHashtable
$lcids = Get-Content -Raw $WindowsLcidPath | ConvertFrom-Json

if (-not $config.ContainsKey("viewTranslation")) {
    throw "Invalid config: missing viewTranslation section."
}

$viewTranslation = $config["viewTranslation"]
$templates = Convert-LcidSectionToMap -Section $viewTranslation["templates"]
if (-not $templates.ContainsKey("1033")) {
    throw "Invalid config: templates.1033 missing."
}

$englishTemplates = Resolve-TemplateSet -AllTemplates $templates -Lcid "1033" -Visiting @{}
$ruleKeys = @($englishTemplates.Keys)

$targetLcids = @($templates.Keys | Where-Object { $_ -ne "1033" })
if ($IncludeLcid.Count -gt 0) {
    $targetLcids = @($targetLcids | Where-Object { $IncludeLcid -contains $_ })
}
$targetLcids = @($targetLcids | Sort-Object {[int]$_})

$lcidNameMap = @{}
foreach ($c in $lcids) {
    $lcidNameMap[[string]$c.lcid] = if ([string]::IsNullOrWhiteSpace($c.englishName)) { $c.displayName } else { $c.englishName }
}

Write-Host "Target LCIDs to process: $($targetLcids.Count)"

$processed = 0
foreach ($lcid in $targetLcids) {
    $processed++

    if (-not $templates.ContainsKey($lcid)) {
        continue
    }

    $languageName = if ($lcidNameMap.ContainsKey($lcid)) { $lcidNameMap[$lcid] } else { "LCID $lcid" }
    $targetTemplateSet = Resolve-TemplateSet -AllTemplates $templates -Lcid $lcid -Visiting @{}

    # Translate only placeholder/default values still matching English baseline.
    $toTranslate = @{}
    foreach ($rule in $ruleKeys) {
        if (-not $targetTemplateSet.ContainsKey($rule)) {
            continue
        }

        $targetRule = $targetTemplateSet[$rule]
        $englishRule = $englishTemplates[$rule]
        if ($null -eq $targetRule -or $null -eq $englishRule) {
            continue
        }

        $targetDefault = [string]$targetRule["default"]
        $englishDefault = [string]$englishRule["default"]

        if ([string]::IsNullOrWhiteSpace($targetDefault) -or [string]::IsNullOrWhiteSpace($englishDefault)) {
            continue
        }

        if ($targetDefault -ceq $englishDefault) {
            $toTranslate[$rule] = $englishDefault
        }
    }

    if ($toTranslate.Count -eq 0) {
        Write-Host "[$processed/$($targetLcids.Count)] LCID $lcid ($languageName): already curated, skipped."
        continue
    }

    Write-Host "[$processed/$($targetLcids.Count)] LCID $lcid ($languageName): translating $($toTranslate.Count) rules..."

    $chunks = @()
    $chunk = @{}
    $i = 0
    foreach ($key in $toTranslate.Keys) {
        $chunk[$key] = $toTranslate[$key]
        $i++
        if ($i -ge $BatchSize) {
            $chunks += ,$chunk
            $chunk = @{}
            $i = 0
        }
    }
    if ($chunk.Count -gt 0) {
        $chunks += ,$chunk
    }

    foreach ($batch in $chunks) {
        $translated = Invoke-TranslationBatch -Endpoint $DeploymentEndpoint -BearerToken $ApiKey -TargetLanguageName $languageName -Items $batch

        foreach ($rule in $batch.Keys) {
            if (-not $translated.ContainsKey($rule)) {
                Write-Warning "LCID ${lcid}: missing translated key '$rule' in response."
                continue
            }

            $value = [string]$translated[$rule]
            if ([string]::IsNullOrWhiteSpace($value)) {
                Write-Warning "LCID ${lcid}: empty translation for '$rule'."
                continue
            }
            if ($value -notmatch "\{entityPlural\}") {
                Write-Warning "LCID ${lcid}: translation for '$rule' does not preserve {entityPlural}, skipped."
                continue
            }

            $targetTemplateSet[$rule]["default"] = $value
        }

        if ($PauseMilliseconds -gt 0) {
            Start-Sleep -Milliseconds $PauseMilliseconds
        }
    }
}

# Persist updated config
$viewTranslation["templates"] = Convert-LcidMapToArray -Map $templates

$json = $config | ConvertTo-Json -Depth 30
Set-Content -Path $ConfigPath -Value $json -Encoding UTF8

Write-Host "Completed. Updated: $ConfigPath"
