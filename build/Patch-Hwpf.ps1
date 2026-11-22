$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../legacy-reader/upstream')).Path
$commit = & git -C $root rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $commit.Trim() -ne '08e5459de5d71b5378d7810506e3919119444632') { throw 'Unexpected HWPF source revision' }
$relative = 'scratchpad/HWPFCore/Model/CHPBinTable.cs'
$blob = & git -C $root hash-object $relative
if ($LASTEXITCODE -ne 0 -or $blob.Trim() -ne '27d0a0138bec6444d71e08fdee88ace432077616') { throw 'Unexpected CHPBinTable source content' }
$path = Join-Path $root $relative
$text = [IO.File]::ReadAllText($path).Replace("`r`n","`n")
$before = @'
            for (List<CHPX>.Enumerator iterator = _textRuns.GetEnumerator(); iterator
                    .MoveNext(); )
            {
                CHPX current = iterator.Current;
'@
$after = @'
            // Modified by Elansary: keep enumeration stable while merging live runs.
            foreach (CHPX current in new List<CHPX>(_textRuns))
            {
'@
$before = $before.Replace("`r`n","`n")
$after = $after.Replace("`r`n","`n")
$count = [regex]::Matches($text,[regex]::Escape($before)).Count
if ($count -ne 1) { throw "Expected compaction loop exactly once; found $count matches after LF normalization" }
$text = $text.Replace($before,$after)
if ($text.Contains($before) -or !$text.Contains($after)) { throw 'HWPF patch postcondition failed' }
[IO.File]::WriteAllText($path,$text,[Text.UTF8Encoding]::new($false))
Write-Host 'Applied snapshot-enumeration compaction fix to pinned HWPF source.'
