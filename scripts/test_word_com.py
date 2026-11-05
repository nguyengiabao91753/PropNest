import subprocess

ps_script = """
try {
    $word = New-Object -ComObject Word.Application
    Write-Output "Word version: $($word.Version)"
    $word.Quit()
} catch {
    Write-Output "Word COM not available: $_"
}
"""
with open("scratch/check_word.ps1", "w", encoding="utf-8") as f:
    f.write(ps_script)

res = subprocess.run(["powershell", "-ExecutionPolicy", "Bypass", "-File", "scratch/check_word.ps1"], capture_output=True, text=True)
print("STDOUT:", res.stdout)
print("STDERR:", res.stderr)
