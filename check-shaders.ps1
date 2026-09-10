param([string]$FxcPath = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\fxc.exe')
$ErrorActionPreference = 'Stop'
$binary = Join-Path $PSScriptRoot 'obj\Release\net10.0-windows10.0.19041.0\GlitchChroma.cso'
$assembly = (& $FxcPath /dumpbin $binary | Out-String)
if ($LASTEXITCODE -ne 0) { throw "Cannot inspect $binary" }
foreach ($semantic in @('SCENE_POSITION', 'TEXCOORD')) {
    if ($assembly -notmatch $semantic) { throw "GlitchChroma has no $semantic input" }
}
$layout = @(
    @('glitchAmount', 0), @('glitchBandSize', 4), @('glitchMaxShift', 8), @('glitchSeed', 12),
    @('chromaticAberration', 16), @('scanlineAmount', 20), @('scanlineSpacing', 24), @('vignetteAmount', 28),
    @('saturation', 32), @('hueRotate', 36), @('brightness', 40), @('padding0', 44),
    @('inputBounds', 48)
)
foreach ($entry in $layout) {
    $field, $offset = $entry
    if ($assembly -notmatch "float[1-4]?\s+$field;\s+// Offset:\s+$offset\s") {
        throw "GlitchChroma constant $field is not at byte offset $offset"
    }
}
Write-Output "PASS: GlitchChroma constant buffer layout"
Write-Output "PASS: GlitchChroma receives Direct2D coordinates"
