$bytes = [System.IO.File]::ReadAllBytes('docs/assets/icon.ico')
$type  = [BitConverter]::ToInt16($bytes, 2)
$count = [BitConverter]::ToInt16($bytes, 4)
Write-Host ('ICO type=' + $type + '  image count=' + $count)
for ($i = 0; $i -lt $count; $i++) {
    $base = 6 + $i * 16
    $w    = $bytes[$base];     if ($w -eq 0) { $w = 256 }
    $h    = $bytes[$base + 1]; if ($h -eq 0) { $h = 256 }
    $bits = [BitConverter]::ToInt16($bytes, $base + 6)
    $sz   = [BitConverter]::ToInt32($bytes, $base + 8)
    Write-Host ('  [' + $i + '] ' + $w + 'x' + $h + '  ' + $bits + 'bpp  ' + $sz + ' bytes')
}
