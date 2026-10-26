$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$path = Join-Path $root 'docs\PROFILE-CHANGELOG.md'
$profilesReadme = Get-Content -LiteralPath (Join-Path $root 'profiles\README.md') -Raw
$text = Get-Content -LiteralPath $path -Raw

if ($text -notmatch '<Aircraft>.*<Profile version>' -or
    $text -notmatch '\*\*Change:\*\*' -or
    $text -notmatch '\*\*Improves:\*\*' -or
    $text -notmatch '\*\*Test:\*\*') {
    throw 'Profile change log must define the concise Change, Improves, Test entry format.'
}

foreach ($entry in [regex]::Matches($text, '(?ms)^## (?!<)(.+?)$(.*?)(?=^## |\z)')) {
    foreach ($field in @('Change', 'Improves', 'Test')) {
        if ($entry.Groups[2].Value -notmatch ('\*\*' + $field + ':\*\*')) {
            throw "Profile change log entry '$($entry.Groups[1].Value)' is missing $field."
        }
    }
}

if ($profilesReadme -notmatch 'PROFILE-CHANGELOG.md') {
    throw 'Profiles README must link the standard profile change log.'
}

Write-Host 'Profile change log contract passed.'
