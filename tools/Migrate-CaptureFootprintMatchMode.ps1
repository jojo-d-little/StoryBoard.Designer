[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory, ValueFromRemainingArguments)]
    [string[]]$Path
)

$ErrorActionPreference = 'Stop'
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$updatedTotal = 0

function Add-AnyOverlap([System.Text.Json.Nodes.JsonNode]$Node) {
    $updated = 0
    if ($Node -is [System.Text.Json.Nodes.JsonObject]) {
        $payloadType = $null
        $hasMatchMode = $false
        foreach ($property in $Node) {
            if ([string]::Equals($property.Key, '$payloadType', [StringComparison]::OrdinalIgnoreCase) -and $property.Value -is [System.Text.Json.Nodes.JsonValue]) {
                $payloadType = $property.Value.GetValue[string]()
            }
            if ([string]::Equals($property.Key, 'footprintMatchMode', [StringComparison]::OrdinalIgnoreCase)) {
                $hasMatchMode = $true
            }
        }
        if ([string]::Equals($payloadType, 'captureObjectsOnObjectFootprint', [StringComparison]::OrdinalIgnoreCase) -and -not $hasMatchMode) {
            $Node['footprintMatchMode'] = 'AnyOverlap'
            $updated++
        }
        $children = [System.Collections.Generic.List[System.Text.Json.Nodes.JsonNode]]::new()
        foreach ($property in $Node) {
            if ($null -ne $property.Value) { $children.Add($property.Value) }
        }
        foreach ($child in $children) {
            $updated += Add-AnyOverlap $child
        }
    }
    elseif ($Node -is [System.Text.Json.Nodes.JsonArray]) {
        foreach ($child in $Node) {
            if ($null -ne $child) { $updated += Add-AnyOverlap $child }
        }
    }
    return $updated
}

foreach ($inputPath in $Path) {
    $resolved = Resolve-Path -LiteralPath $inputPath
    $item = Get-Item -LiteralPath $resolved.Path
    $files = if ($item.PSIsContainer) {
        Get-ChildItem -LiteralPath $item.FullName -Recurse -File -Filter '*.json'
    } else {
        @($item)
    }

    foreach ($file in $files) {
        $text = [System.IO.File]::ReadAllText($file.FullName)
        $document = [System.Text.Json.Nodes.JsonNode]::Parse($text)
        if ($null -eq $document) { continue }
        $updated = Add-AnyOverlap $document
        if ($updated -gt 0) {
            $updatedTotal += $updated
            if ($PSCmdlet.ShouldProcess($file.FullName, "Write AnyOverlap to $updated capture payload(s)")) {
                $options = [System.Text.Json.JsonSerializerOptions]::new()
                $options.WriteIndented = $text.Contains("`n")
                [System.IO.File]::WriteAllText($file.FullName, $document.ToJsonString($options), $utf8NoBom)
            }
            Write-Output "$($file.FullName): $updated capture payload(s) need migration"
        }
    }
}

Write-Output "Total capture payloads needing migration: $updatedTotal"
