
try {
    $word = New-Object -ComObject Word.Application
    Write-Output "Word version: $($word.Version)"
    $word.Quit()
} catch {
    Write-Output "Word COM not available: $_"
}
