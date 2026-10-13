Set-StrictMode -Version Latest

function Get-SavedGamesRoot {
    $registryPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders'
    $savedGamesId = '{4C5C32FF-BB9D-43B0-BF7B-1DDE8B8C7D1E}'

    try {
        $value = (Get-ItemProperty -LiteralPath $registryPath -Name $savedGamesId -ErrorAction Stop).$savedGamesId
        if ($value) {
            return [Environment]::ExpandEnvironmentVariables($value)
        }
    }
    catch {
        # Fall back to the standard location.
    }

    return (Join-Path $env:USERPROFILE 'Saved Games')
}

function Find-DcsProfiles {
    param([string[]]$RequestedPaths)

    if ($RequestedPaths) {
        return @($RequestedPaths | ForEach-Object {
            if (-not (Test-Path -LiteralPath $_ -PathType Container)) {
                throw "DCS profile folder does not exist: $_"
            }
            (Resolve-Path -LiteralPath $_).Path
        } | Sort-Object -Unique)
    }

    $savedGamesRoot = Get-SavedGamesRoot
    if (-not (Test-Path -LiteralPath $savedGamesRoot -PathType Container)) {
        return @()
    }

    return @(Get-ChildItem -LiteralPath $savedGamesRoot -Directory | Where-Object {
        $_.Name -eq 'DCS' -or $_.Name -like 'DCS.*'
    } | ForEach-Object { $_.FullName } | Sort-Object -Unique)
}

function Find-SimHubPath {
    param([string]$RequestedPath)

    $candidates = @(
        $RequestedPath,
        $env:SIMHUB_INSTALL_PATH,
        $(if (${env:ProgramFiles(x86)}) { Join-Path ${env:ProgramFiles(x86)} 'SimHub' }),
        $(if ($env:ProgramFiles) { Join-Path $env:ProgramFiles 'SimHub' })
    ) | Where-Object { $_ }

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath (Join-Path $candidate 'SimHubWPF.exe') -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw 'SimHub was not found. Pass -SimHubPath with the folder containing SimHubWPF.exe.'
}

function Get-HashRecords {
    param(
        [Parameter(Mandatory)]
        [string]$Root,

        [string[]]$ExcludedPaths,

        [switch]$Recurse
    )

    if (-not (Test-Path -LiteralPath $Root -PathType Container)) {
        return @()
    }

    $resolvedRoot = (Resolve-Path -LiteralPath $Root).Path
    $excluded = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($path in @($ExcludedPaths)) {
        if ($path) {
            [void]$excluded.Add([IO.Path]::GetFullPath($path))
        }
    }

    $parameters = @{
        LiteralPath = $resolvedRoot
        File = $true
    }
    if ($Recurse) {
        $parameters.Recurse = $true
    }

    return @(Get-ChildItem @parameters | Where-Object {
        -not $excluded.Contains($_.FullName)
    } | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{
            Path = $_.FullName
            Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    })
}

function Compare-HashRecords {
    param(
        [Parameter(Mandatory)]
        [object[]]$Before,

        [Parameter(Mandatory)]
        [object[]]$After
    )

    $beforeMap = @{}
    $afterMap = @{}
    foreach ($record in $Before) { $beforeMap[$record.Path.ToLowerInvariant()] = $record }
    foreach ($record in $After) { $afterMap[$record.Path.ToLowerInvariant()] = $record }

    $differences = [System.Collections.Generic.List[object]]::new()
    foreach ($key in $beforeMap.Keys) {
        if (-not $afterMap.ContainsKey($key)) {
            $differences.Add([pscustomobject]@{ Status = 'Missing'; Path = $beforeMap[$key].Path })
        }
        elseif ($beforeMap[$key].Sha256 -ne $afterMap[$key].Sha256) {
            $differences.Add([pscustomobject]@{ Status = 'Changed'; Path = $beforeMap[$key].Path })
        }
    }

    foreach ($key in $afterMap.Keys) {
        if (-not $beforeMap.ContainsKey($key)) {
            $differences.Add([pscustomobject]@{ Status = 'Added'; Path = $afterMap[$key].Path })
        }
    }

    return @($differences | Sort-Object Path)
}
