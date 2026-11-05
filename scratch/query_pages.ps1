param([string]$docPath)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$word = New-Object -ComObject Word.Application
$word.Visible = $false
try {
    $doc = $word.Documents.Open($docPath)
    Write-Output "Document opened. Page count: $($doc.ComputeStatistics(2))"
    for ($i = 1; $i -le $doc.Paragraphs.Count; $i++) {
        $p = $doc.Paragraphs.Item($i)
        $txt = $p.Range.Text.Trim()
        if ($txt.Length -gt 0) {
            $physPg = $p.Range.Information(3) # wdActiveEndPageNumber
            $adjPg = $p.Range.Information(1)  # wdActiveEndAdjustedPageNumber
            $sectIdx = $p.Range.Sections.Item(1).Index
            Write-Output "Sec $sectIdx | PhysPg $physPg | AdjPg $adjPg | $txt"
        }
    }
    $doc.Close([ref]$false)
} finally {
    $word.Quit()
}
