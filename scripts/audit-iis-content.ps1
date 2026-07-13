[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$LogRoot,

    [Parameter(Mandatory = $true)]
    [string]$ContentRoot,

    [ValidateRange(1, 3650)]
    [int]$Days = 90,

    [string[]]$CandidateRoots = @('_preview', 'TempFile', 'Award', 'js', 'layui', 'Filme'),

    [string]$PathOutput = (Join-Path $env:TEMP 'szaipa-content-paths.txt')
)

$ErrorActionPreference = 'Stop'
$windowStart = (Get-Date).Date.AddDays(-$Days)
$stats = @{}
foreach ($root in $CandidateRoots) {
    $stats[$root] = @{
        Requests = 0L
        Success = 0L
        Paths = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
        Top = @{}
    }
}

$allPaths = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$files = @(
    Get-ChildItem -LiteralPath $LogRoot -Filter 'u_ex*.log' -File |
        Where-Object { $_.LastWriteTime -ge $windowStart } |
        Sort-Object Name
)
$lineCount = 0L
$skippedFiles = 0
$malformedPaths = 0L

foreach ($file in $files) {
    $fieldMap = $null
    $stream = $null
    $reader = $null
    try {
        # IIS keeps today's log open. Share the handle so the audit stays read-only
        # and can include the current file without interrupting the site.
        $stream = [IO.FileStream]::new(
            $file.FullName,
            [IO.FileMode]::Open,
            [IO.FileAccess]::Read,
            [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete
        )
        $reader = [IO.StreamReader]::new($stream)

        while ($null -ne ($line = $reader.ReadLine())) {
            if ($line.StartsWith('#Fields:')) {
                $names = $line.Substring(8).Trim() -split '\s+'
                $fieldMap = @{}
                for ($index = 0; $index -lt $names.Count; $index++) {
                    $fieldMap[$names[$index]] = $index
                }
                continue
            }

            if ($line.StartsWith('#') -or $null -eq $fieldMap) { continue }
            if (-not $fieldMap.ContainsKey('cs-method') -or
                -not $fieldMap.ContainsKey('cs-uri-stem') -or
                -not $fieldMap.ContainsKey('sc-status')) { continue }

            $parts = $line -split '\s+'
            if ($parts.Count -lt $fieldMap.Count) { continue }
            $lineCount++

            $method = $parts[$fieldMap['cs-method']]
            if ($method -ne 'GET' -and $method -ne 'HEAD') { continue }

            try {
                $path = [Uri]::UnescapeDataString($parts[$fieldMap['cs-uri-stem']])
            }
            catch {
                $malformedPaths++
                continue
            }

            if (-not $path.StartsWith('/Content/', [StringComparison]::OrdinalIgnoreCase)) { continue }
            [void]$allPaths.Add($path)

            $status = 0
            [int]::TryParse($parts[$fieldMap['sc-status']], [ref]$status) | Out-Null
            $relative = $path.Substring('/Content/'.Length)
            $root = $relative.Split('/')[0]
            if (-not $stats.ContainsKey($root)) { continue }

            $item = $stats[$root]
            $item.Requests++
            if ($status -ge 200 -and $status -lt 400) { $item.Success++ }
            [void]$item.Paths.Add($path)
            if (-not $item.Top.ContainsKey($path)) { $item.Top[$path] = 0L }
            $item.Top[$path]++
        }
    }
    catch [IO.IOException] {
        $skippedFiles++
    }
    finally {
        if ($null -ne $reader) { $reader.Dispose() }
        elseif ($null -ne $stream) { $stream.Dispose() }
    }
}

[IO.File]::WriteAllLines(
    $PathOutput,
    [string[]]($allPaths | Sort-Object),
    [Text.UTF8Encoding]::new($false)
)

Write-Output ('WINDOW_START=' + $windowStart.ToString('yyyy-MM-dd'))
Write-Output ('LOG_FILES=' + $files.Count)
Write-Output ('SKIPPED_FILES=' + $skippedFiles)
Write-Output ('LOG_LINES=' + $lineCount)
Write-Output ('MALFORMED_PATHS=' + $malformedPaths)
Write-Output ('UNIQUE_CONTENT_PATHS=' + $allPaths.Count)
Write-Output ('PATH_FILE=' + $PathOutput)

foreach ($root in $CandidateRoots) {
    $item = $stats[$root]
    $rootPath = Join-Path $ContentRoot $root
    $physicalFiles = if (Test-Path -LiteralPath $rootPath) {
        @(Get-ChildItem -LiteralPath $rootPath -Recurse -File -Force -ErrorAction SilentlyContinue)
    }
    else { @() }

    $totalBytes = [long](($physicalFiles | Measure-Object -Property Length -Sum).Sum)
    $matchedFiles = 0
    $matchedBytes = 0L
    foreach ($physicalFile in $physicalFiles) {
        $relativePath = $physicalFile.FullName.Substring($ContentRoot.Length).TrimStart('\').Replace('\', '/')
        if ($allPaths.Contains('/Content/' + $relativePath)) {
            $matchedFiles++
            $matchedBytes += $physicalFile.Length
        }
    }

    $latestUtc = if ($physicalFiles.Count -gt 0) {
        ($physicalFiles |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -First 1).LastWriteTimeUtc.ToString('yyyy-MM-ddTHH:mm:ssZ')
    }
    else { '' }

    Write-Output (
        'ROOT=' + $root +
        ';REQUESTS=' + $item.Requests +
        ';SUCCESS=' + $item.Success +
        ';UNIQUE=' + $item.Paths.Count +
        ';FILES=' + $physicalFiles.Count +
        ';BYTES=' + $totalBytes +
        ';MATCHED_FILES=' + $matchedFiles +
        ';MATCHED_BYTES=' + $matchedBytes +
        ';LATEST_UTC=' + $latestUtc
    )

    $item.Top.GetEnumerator() |
        Sort-Object Value -Descending |
        Select-Object -First 5 |
        ForEach-Object {
            Write-Output ('TOP=' + $root + ';COUNT=' + $_.Value + ';PATH=' + $_.Key)
        }
}
