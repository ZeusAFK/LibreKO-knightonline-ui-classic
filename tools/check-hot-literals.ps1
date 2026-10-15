<#
  Guard for per-frame code: lists string literals passed to Godot meta, theme, node-path and group APIs inside
  methods that run every frame (or on every refresh): _Process, _PhysicsProcess, _Draw and methods whose names
  start with Sync, Refresh, Tick, Mirror, UpdateSource, UpdateFrame or StyleRows.

  Each such literal creates a finalizable Godot StringName or NodePath per call; per frame that garbage makes every
  .NET GC pause long (research: PLUGIN-AUDIT M2, PROFILE section 4). Use a static readonly StringName/NodePath
  (Layout/ThemeNames.cs holds the common theme names) instead.

  Usage:  pwsh tools/check-hot-literals.ps1 [-Root src] [-Allow tools/hot-literals-allow.txt]
  Exit code 1 when a finding is not on the allow list (one "File.cs:Method:literal" per line).
#>
param(
    [string]$Root = (Join-Path $PSScriptRoot '..\src'),
    [string]$Allow = (Join-Path $PSScriptRoot 'hot-literals-allow.txt')
)
$ErrorActionPreference = 'Stop'
$hot = '^(?:_Process|_PhysicsProcess|_Draw|Sync\w*|Refresh\w*|Tick\w*|Mirror\w*|UpdateSource|UpdateFrame|StyleRows)$'
$api = '\b(?:GetMeta|HasMeta|SetMeta|RemoveMeta|GetTheme\w+|HasTheme\w+|AddTheme\w+Override|RemoveTheme\w+Override|GetNode(?:OrNull)?(?:<[^>]+>)?|HasNode|IsInGroup|AddToGroup|GetNodesInGroup)\s*\(\s*"([^"]*)"'
$allowed = @{}
if (Test-Path $Allow) { Get-Content $Allow | Where-Object { $_ -and -not $_.StartsWith('#') } | ForEach-Object { $allowed[$_.Trim()] = $true } }
$findings = @()
foreach ($file in Get-ChildItem $Root -Recurse -Filter *.cs) {
    $text = Get-Content $file.FullName -Raw
    # Method headers: a member name followed by a parameter list and a body or an expression body.
    foreach ($m in [regex]::Matches($text, '(?m)^\s*(?:(?:public|private|protected|internal|static|override|virtual|sealed|async|new)\s+)+[\w<>\[\],.? ]+?\s+(\w+)\s*\([^)]*\)\s*(=>|\{)')) {
        $name = $m.Groups[1].Value
        if ($name -notmatch $hot) { continue }
        $start = $m.Index + $m.Length
        if ($m.Groups[2].Value -eq '=>') {
            $end = $text.IndexOf(';', $start)
            $body = $text.Substring($start, [Math]::Max(0, $end - $start))
        } else {
            $depth = 1; $i = $start
            while ($i -lt $text.Length -and $depth -gt 0) {
                $c = $text[$i]
                if ($c -eq '{') { $depth++ } elseif ($c -eq '}') { $depth-- }
                $i++
            }
            $body = $text.Substring($start, $i - $start)
        }
        foreach ($hit in [regex]::Matches($body, $api)) {
            $line = ($text.Substring(0, $start + $hit.Index) -split "`n").Count
            $key = "$($file.Name):${name}:$($hit.Groups[1].Value)"
            if ($allowed.ContainsKey($key)) { continue }
            $findings += [pscustomobject]@{ File = $file.FullName.Substring((Resolve-Path $Root).Path.Length + 1); Line = $line; Method = $name; Literal = $hit.Groups[1].Value }
        }
    }
}
if ($findings.Count -eq 0) { 'check-hot-literals: no string literal reaches a Godot name API in per-frame code.'; exit 0 }
$findings | Sort-Object File, Line | Format-Table -AutoSize | Out-String -Width 200
"check-hot-literals: $($findings.Count) finding(s)."
exit 1
