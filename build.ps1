param([string]$PackageRoot = $PSScriptRoot)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $PackageRoot).Path
$source = Join-Path $PSScriptRoot 'PhotoKit.cs'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw "Missing .NET Framework compiler: $compiler" }
if (!(Test-Path -LiteralPath $source)) { throw "Missing launcher source: $source" }

$rows = @(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
    $_.FullName -notmatch '\\(\.git|bin|obj)\\' -and
    ($_.FullName.Substring($root.Length + 1) -match '^components\\(bossarena|me3|crittercam)\\' -or
     $_.FullName.Substring($root.Length + 1) -match '^runtimes\\[^\\]+\.exe$')
} | Sort-Object FullName | ForEach-Object {
    $relative = $_.FullName.Substring($root.Length + 1)
    (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash + '  ' + $relative
})
if ($rows.Count -lt 10) { throw 'Component tree is incomplete. See README.md for the required upstream components.' }

$manifest = Join-Path $root 'required.sha256'
$manifestTemp = $manifest + '.tmp'
$output = Join-Path $root '启动工具包.exe'
$outputTemp = Join-Path $root ('launcher-' + [Guid]::NewGuid().ToString('N') + '.exe')
[IO.File]::WriteAllLines($manifestTemp, [string[]]$rows, (New-Object Text.UTF8Encoding($false)))
try {
    & $compiler /nologo /target:winexe /platform:x64 /optimize+ /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll "/resource:$manifestTemp,required.sha256" "/out:$outputTemp" $source
    if ($LASTEXITCODE -ne 0) { throw 'Launcher compilation failed.' }
    if (Test-Path -LiteralPath $output) {
        [IO.File]::Replace($outputTemp, $output, $output + '.rollback')
        Remove-Item -LiteralPath ($output + '.rollback')
    } else { [IO.File]::Move($outputTemp, $output) }
    Write-Output "Built launcher and embedded $($rows.Count) component checksums."
} finally {
    Remove-Item -LiteralPath $manifestTemp,$outputTemp -Force -ErrorAction SilentlyContinue
}
