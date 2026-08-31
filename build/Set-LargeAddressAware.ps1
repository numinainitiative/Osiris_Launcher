[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$Path,

    [switch]$VerifyOnly
)

$ErrorActionPreference = 'Stop'
$imagePath = [IO.Path]::GetFullPath($Path)
$readOnly = [bool]$VerifyOnly
$access = if ($readOnly) { [IO.FileAccess]::Read } else { [IO.FileAccess]::ReadWrite }
$share = if ($readOnly) { [IO.FileShare]::ReadWrite } else { [IO.FileShare]::Read }
$stream = [IO.File]::Open($imagePath, [IO.FileMode]::Open, $access, $share)

try {
    $reader = [IO.BinaryReader]::new($stream, [Text.Encoding]::ASCII, $true)
    if ($stream.Length -lt 256 -or $reader.ReadUInt16() -ne 0x5A4D) {
        throw "Not a valid PE image: $imagePath"
    }

    $stream.Position = 0x3C
    $peOffset = $reader.ReadInt32()
    if ($peOffset -lt 0x40 -or $peOffset -gt ($stream.Length - 26)) {
        throw "Invalid PE header offset in: $imagePath"
    }

    $stream.Position = $peOffset
    if ($reader.ReadUInt32() -ne 0x00004550) {
        throw "Missing PE signature in: $imagePath"
    }

    $machine = $reader.ReadUInt16()
    if ($machine -ne 0x014C) {
        throw ('Expected a 32-bit x86 executable, found machine 0x{0:X4}: {1}' -f $machine, $imagePath)
    }

    $stream.Position = $peOffset + 24
    if ($reader.ReadUInt16() -ne 0x010B) {
        throw "Expected a PE32 optional header: $imagePath"
    }

    $characteristicsOffset = $peOffset + 22
    $stream.Position = $characteristicsOffset
    $characteristics = $reader.ReadUInt16()
    $isLargeAddressAware = ($characteristics -band 0x0020) -ne 0

    if ($VerifyOnly) {
        if (-not $isLargeAddressAware) {
            throw "LARGE_ADDRESS_AWARE is not enabled: $imagePath"
        }
    }
    elseif (-not $isLargeAddressAware -and $PSCmdlet.ShouldProcess($imagePath, 'Enable LARGE_ADDRESS_AWARE')) {
        $stream.Position = $characteristicsOffset
        $writer = [IO.BinaryWriter]::new($stream, [Text.Encoding]::ASCII, $true)
        $writer.Write([UInt16]($characteristics -bor 0x0020))
        $writer.Flush()
        $stream.Flush($true)
        $isLargeAddressAware = $true
    }

    [pscustomobject]@{
        Path = $imagePath
        Machine = ('0x{0:X4}' -f $machine)
        LargeAddressAware = $isLargeAddressAware
        Changed = (-not $VerifyOnly -and (($characteristics -band 0x0020) -eq 0) -and $isLargeAddressAware)
    }
}
finally {
    $stream.Dispose()
}
