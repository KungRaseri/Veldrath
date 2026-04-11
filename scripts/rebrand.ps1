param(
    [string]$Root = "C:\code\RealmEngine"
)

$pairs = @(
    ,"RealmUnbound.Server.Tests","Veldrath.Server.Tests"
    ,"RealmUnbound.Client.Tests","Veldrath.Client.Tests"
    ,"RealmUnbound.Discord.Tests","Veldrath.Discord.Tests"
    ,"RealmUnbound.Assets.Tests","Veldrath.Assets.Tests"
    ,"RealmUnbound.Server","Veldrath.Server"
    ,"RealmUnbound.Client","Veldrath.Client"
    ,"RealmUnbound.Contracts","Veldrath.Contracts"
    ,"RealmUnbound.Discord","Veldrath.Discord"
    ,"RealmUnbound.Assets","Veldrath.Assets"
    ,"realmunbound-server","veldrath-server"
    ,"realmunbound-discord","veldrath-discord"
    ,"RealmUnbound-Server","Veldrath-Server"
    ,"RealmUnbound-Client","Veldrath-Client"
    ,"RealmUnbound-Discord","Veldrath-Discord"
    ,"RealmUnbound-Assets","Veldrath-Assets"
    ,"realmunbound","veldrath"
    ,"RealmUnbound","Veldrath"
    ,"Draveth","Veldrath"
    ,"draveth","veldrath"
)

# Build ordered list of [from, to] pairs
$replacements = New-Object System.Collections.Generic.List[object]
for ($i = 0; $i -lt $pairs.Count; $i += 2) {
    $replacements.Add(@($pairs[$i], $pairs[$i+1]))
}

$validExts = @(".cs",".csproj",".slnx",".json",".yml",".yaml",".xml",".axaml",".xaml",".razor",".md",".runsettings",".props",".targets",".txt")
$excludePattern = '[\\/](logs|coverage-results|package|\.git|\.vs|TestResults|bin|obj|wiki)[\\/]'

$files = Get-ChildItem -Path $Root -Recurse -File | Where-Object {
    ($_.Extension -in $validExts -or $_.Name -eq "Dockerfile") -and
    ($_.FullName -notmatch $excludePattern)
}

Write-Host "Scanning $($files.Count) candidate files..."

$count = 0
foreach ($file in $files) {
    $original = [System.IO.File]::ReadAllText($file.FullName)
    $updated = $original
    foreach ($pair in $replacements) {
        $updated = $updated.Replace($pair[0], $pair[1])
    }
    if ($updated -ne $original) {
        [System.IO.File]::WriteAllText($file.FullName, $updated, [System.Text.Encoding]::UTF8)
        $count++
        Write-Host "  Updated: $($file.FullName.Replace($Root, '.'))"
    }
}

Write-Host ""
Write-Host "Done -- $count files updated."
