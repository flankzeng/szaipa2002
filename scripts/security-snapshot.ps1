<#
.SYNOPSIS
    Aggregates the Windows Security log (4625 failed logons / 4740 lockouts) into security_snapshot.json
    for the staff security dashboard, and applies staff-issued unblock / whitelist-edit requests that were
    queued by the web app.

.DESCRIPTION
    Designed to run as a SYSTEM scheduled task every 5 minutes. It is the ONLY privileged actor in the
    security-monitoring design:

      * It reads the Security event log (requires elevation — SYSTEM has it).
      * It writes C:\Security\security_snapshot.json (UTF-8 BOM) that the IIS web app reads (no elevation).
      * It consumes C:\Security\unblock_queue\*.txt and C:\Security\whitelist_edit_queue\*.txt dropped by
        the staff dashboard, runs netsh, and edits blocked_ips.csv / whitelist.txt.
      * It never touches the database or the web app.

    Event property indices below mirror the AutoBlock.ps1 mapping already verified on this server
    (Windows Server 2019): 4625 source IP = Properties[19], target user = Properties[5]. These indices are
    Windows-version-sensitive; re-verify with `Get-WinEvent -LogName Security -MaxEvents 1` on any new host.

.PARAMETER SecurityRoot
    Root of the server security tree (blocked_ips.csv / whitelist.txt / queues). Default C:\Security.

.PARAMETER WindowHours
    Aggregation window for the snapshot. Default 24.

.EXAMPLE
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\Security\security-snapshot.ps1

.NOTES
    Install as a scheduled task:
      schtasks /Create /TN "SzaipaSecuritySnapshot" /TR "powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\Security\security-snapshot.ps1" /SC MINUTE /MO 5 /RU SYSTEM /RL HIGHEST
#>
[CmdletBinding()]
param(
    [string]$SecurityRoot = 'C:\Security',

    [ValidateRange(1, 168)]
    [int]$WindowHours = 24
)

$ErrorActionPreference = 'Stop'
$snapshotPath = Join-Path $SecurityRoot 'security_snapshot.json'
$blockedCsv = Join-Path $SecurityRoot 'blocked_ips.csv'
$whitelistTxt = Join-Path $SecurityRoot 'whitelist.txt'
$unblockQueue = Join-Path $SecurityRoot 'unblock_queue'
$whitelistQueue = Join-Path $SecurityRoot 'whitelist_edit_queue'

function Write-BomUtf8([string]$Path, [string]$Content) {
    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($true))
}

function Get-FileLines([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return @() }
    return Get-Content -LiteralPath $Path -Encoding UTF8
}

# Ensure the queue directories exist so the web app can always drop request files.
New-Item -ItemType Directory -Force -Path $unblockQueue, $whitelistQueue | Out-Null

$processedUnblocks = 0
$processedWhitelist = 0
$failedQueue = 0

# ============================================================ 1) Snapshot
$snapshot = $null
$failedLogons = 0L
$lockouts = 0L
$byIp = @{}
$byUser = @{}
$windowStart = (Get-Date).AddHours(-$WindowHours)

try {
    $events = Get-WinEvent -FilterHashtable @{ LogName = 'Security'; Id = 4625, 4740; StartTime = $windowStart } -ErrorAction SilentlyContinue
    foreach ($ev in $events) {
        if ($ev.Id -eq 4625) {
            $ip = $ev.Properties[19].Value
            $user = $ev.Properties[5].Value
            if (-not $ip -or $ip -eq '-') { continue }
            $failedLogons++
            if (-not $byIp.ContainsKey($ip)) { $byIp[$ip] = @{ Count = 0L; Users = [System.Collections.Generic.HashSet[string]]::new() } }
            $entry = $byIp[$ip]
            $entry.Count++
            if ($user -and $user -ne '-') { [void]$entry.Users.Add($user) }
        }
        elseif ($ev.Id -eq 4740) {
            $lockouts++
            $user = $ev.Properties[0].Value
            if ($user) {
                if (-not $byUser.ContainsKey($user)) { $byUser[$user] = 0L }
                $byUser[$user]++
            }
        }
    }

    $lastAlertLine = ''
    if (Test-Path -LiteralPath (Join-Path $SecurityRoot 'alert_errors.log')) {
        $lastAlertLine = (Get-Content -LiteralPath (Join-Path $SecurityRoot 'alert_errors.log') -Encoding UTF8 -ErrorAction SilentlyContinue | Where-Object { $_.Trim() } | Select-Object -Last 1)
    }

    $snapshot = [ordered]@{
        schemaVersion   = 1
        generatedAtUtc  = [DateTimeOffset]::UtcNow.ToString('o')
        windowStartUtc  = ([DateTimeOffset]$windowStart).ToString('o')
        failedLogons24h = $failedLogons
        lockouts24h     = $lockouts
        bySourceIp      = @($byIp.GetEnumerator() | ForEach-Object {
            [ordered]@{
                ip          = $_.Key
                failedLogons = $_.Value.Count
                users       = @($_.Value.Users)
                lastSeenUtc = $null
            }
        } | Sort-Object -Property failedLogons -Descending)
        lockoutsByUser  = @($byUser.GetEnumerator() | ForEach-Object {
            [ordered]@{ user = $_.Key; count = $_.Value }
        })
        lastAlert       = $null
    }

    if ($lastAlertLine) {
        $spaceIndex = $lastAlertLine.IndexOf(' ')
        $snapshot.lastAlert = [ordered]@{
            atUtc   = $null
            level   = 'Error'
            message = $lastAlertLine
        }
        if ($spaceIndex -gt 10 -and ($datePart = $lastAlertLine.Substring(0, $spaceIndex))) {
            try { $snapshot.lastAlert.atUtc = ([DateTimeOffset][datetime]::Parse($datePart)).ToString('o') } catch { }
        }
    }

    $json = $snapshot | ConvertTo-Json -Depth 6
    Write-BomUtf8 -Path $snapshotPath -Content $json
    Write-Output "SNAPSHOT_OK failedLogons=$failedLogons lockouts=$lockouts"
}
catch {
    Write-Output "SNAPSHOT_ERROR $($_.Exception.Message)"
}

# ============================================================ 2) Unblock queue
if (Test-Path -LiteralPath $unblockQueue) {
    $pending = Get-ChildItem -LiteralPath $unblockQueue -Filter '*.txt' -File | Where-Object { $_.Name -notlike '*.done.txt' }
    foreach ($file in $pending) {
        $ip = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
        $donePath = Join-Path $unblockQueue ($file.BaseName + '.done.txt')
        $result = 'ok'
        $message = ''
        try {
            # Delete the firewall rule (ignore "no rules match" which returns exit code 1).
            $output = & netsh advfirewall firewall delete rule name="BruteBlock_$ip" 2>&1
            if ($LASTEXITCODE -ne 0 -and $output -notmatch 'No rules match') {
                throw ($output -join ' ')
            }
            # Remove the matching line from blocked_ips.csv (keep header/comments).
            $lines = Get-FileLines -Path $blockedCsv
            $kept = @($lines | Where-Object { $_ -and $_.Trim() -and ($_ -split ',')[0].Trim() -ne $ip })
            Write-BomUtf8 -Path $blockedCsv -Content (($kept -join "`r`n") + "`r`n")
            $message = "已删除 BruteBlock_$ip 规则"
        }
        catch {
            $result = 'error'
            $message = $_.Exception.Message
            $failedQueue++
        }
        Write-BomUtf8 -Path $donePath -Content "$([DateTimeOffset]::UtcNow.ToString('o'))|$result|$message"
        Remove-Item -LiteralPath $file.FullName -Force -ErrorAction SilentlyContinue
        $processedUnblocks++
        Write-Output "UNBLOCK $ip $result"
    }
}

# ============================================================ 3) Whitelist edit queue
if (Test-Path -LiteralPath $whitelistQueue) {
    $pending = Get-ChildItem -LiteralPath $whitelistQueue -Filter '*.txt' -File | Where-Object { $_.Name -notlike '*.done.txt' }
    foreach ($file in $pending) {
        $body = (Get-Content -LiteralPath $file.FullName -Encoding UTF8 -ErrorAction SilentlyContinue | Select-Object -First 1)
        $parts = @($body -split '\|')
        $donePath = Join-Path $whitelistQueue ($file.BaseName + '.done.txt')
        $result = 'ok'
        $message = ''
        try {
            if ($parts.Count -lt 4) { throw '队列文件格式无效' }
            $action = $parts[2].Trim().ToLowerInvariant()
            $ip = $parts[3].Trim()
            $existing = Get-FileLines -Path $whitelistTxt
            if ($action -eq 'add') {
                $already = @($existing | Where-Object { $_.Trim() -and $_.Trim() -ne '' -and $_.Trim()[0] -ne '#' -and $_.Trim() -eq $ip })
                if ($already.Count -eq 0) {
                    Write-BomUtf8 -Path $whitelistTxt -Content ((($existing + $ip) -join "`r`n") + "`r`n")
                    $message = "已添加白名单 $ip"
                }
                else { $message = "白名单已存在 $ip" }
            }
            elseif ($action -eq 'remove') {
                $kept = @($existing | Where-Object { -not ($_.Trim() -and $_.Trim() -ne '' -and $_.Trim()[0] -ne '#' -and $_.Trim() -eq $ip) })
                Write-BomUtf8 -Path $whitelistTxt -Content (($kept -join "`r`n") + "`r`n")
                $message = "已移除白名单 $ip"
            }
            else { throw "未知白名单操作: $action" }
        }
        catch {
            $result = 'error'
            $message = $_.Exception.Message
            $failedQueue++
        }
        Write-BomUtf8 -Path $donePath -Content "$([DateTimeOffset]::UtcNow.ToString('o'))|$result|$message"
        Remove-Item -LiteralPath $file.FullName -Force -ErrorAction SilentlyContinue
        $processedWhitelist++
        Write-Output "WHITELIST $action $ip $result"
    }
}

Write-Output "QUEUE_OK unblocks=$processedUnblocks whitelist=$processedWhitelist failed=$failedQueue"
