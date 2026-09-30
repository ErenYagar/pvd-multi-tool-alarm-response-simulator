$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$errors = @()
$files = @(Get-ChildItem -LiteralPath "$project/Assets" -Filter '*.cs' -Recurse)
foreach ($file in $files) {
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($file.FullName))
    $errors += @($tree.GetDiagnostics() | Where-Object { $_.Severity.ToString() -eq 'Error' } | ForEach-Object { "$($file.Name): $_" })
}
if ($errors.Count) { $errors | Write-Output; exit 1 }
"PASS: $($files.Count) C# files parsed; syntax only, does not replace Unity compilation/rendering." | Tee-Object -FilePath "$project/TestResults/syntax.txt"
