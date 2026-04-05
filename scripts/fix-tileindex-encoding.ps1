$f = 'c:\code\RealmEngine\RealmEngine.Shared\Models\TileIndex.cs'
$c = [System.IO.File]::ReadAllText($f, [System.Text.Encoding]::UTF8)
$r = [char]0xFFFD
$c = $c -replace '// \? verified', '// <- verified'
$c = $c -replace "columns $r 31 rows", 'columns x 31 rows'
$c = $c -replace "Pack $r 57 columns", 'Pack - 57 columns'
$c = $c.Replace([char]0xFFFD, '-')
[System.IO.File]::WriteAllText($f, $c, [System.Text.Encoding]::UTF8)
$c2 = [System.IO.File]::ReadAllText($f, [System.Text.Encoding]::UTF8)
$remaining = ($c2.ToCharArray() | Where-Object { [int]$_ -gt 127 }).Count
Write-Host "Non-ASCII chars remaining: $remaining"
