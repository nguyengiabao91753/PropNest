
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    $word = New-Object -ComObject Word.Application
    $word.Visible = $false
    try {
        $doc = $word.Documents.Open('C:\Users\GiaBao\source\repos\PropNest\docs\Báo_cáo_cá_nhân_NguyenGiaBao.docx')
        $count = $doc.Paragraphs.Count
        for ($i = 1; $i -le $count; $i++) {
            $p = $doc.Paragraphs.Item($i)
            $txt = $p.Range.Text.Trim()
            if ($txt.Length -gt 0) {
                $adjPg = $p.Range.Information(1) # wdActiveEndAdjustedPageNumber
                $sectIdx = $p.Range.Sections.Item(1).Index
                if ($sectIdx -eq 3) {
                    Write-Output "SECTION3|$adjPg|$txt"
                }
            }
        }
        $doc.Close([ref]$false)
    } finally {
        $word.Quit()
    }
    