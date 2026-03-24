$box = [char]0x2500  # ─

$files = Get-ChildItem -Path "." -Recurse -Filter "*.cs" |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }

$count = 0
foreach ($file in $files) {
    $bytes   = [System.IO.File]::ReadAllBytes($file.FullName)
    $content = [System.Text.Encoding]::UTF8.GetString($bytes)

    if (-not $content.Contains($box)) { continue }

    # Section header with label:  // ── Foo ─────...  →  // Foo
    $new = [regex]::Replace($content, "(?m)([ \t]*//\s*)[$box]+\s*([^$box\r\n]+?)\s*[$box]*\s*`$", '$1$2')
    # Pure divider line:  // ─────...  (no label)  → remove the line
    $new = [regex]::Replace($new, "(?m)[ \t]*//\s*[$box]+[ \t]*\r?\n", "")

    if ($new -ne $content) {
        [System.IO.File]::WriteAllBytes($file.FullName, [System.Text.Encoding]::UTF8.GetBytes($new))
        $count++
        Write-Host "Updated: $($file.Name)"
    }
}

Write-Host ""
Write-Host "Total files updated: $count"
