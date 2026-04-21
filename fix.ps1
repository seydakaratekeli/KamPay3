$files = Get-ChildItem -Path "KamPay\Views" -Recurse -Filter "*.xaml"
foreach ($f in $files) {
    if ($f.Name -match "RegisterPage|ProductListPage") { continue }
    $c = Get-Content $f.FullName -Raw
    $orig = $c
    $c = [regex]::Replace($c, '\{Binding\s+Source=\{x:Static\s+[a-zA-Z0-9_]+:LocalizationResourceManager\.Instance\},\s*Path=\[([a-zA-Z0-9_]+)\]\}', '{extensions:Translate ${1}}')
    $c = [regex]::Replace($c, '\{Binding\s+\[([a-zA-Z0-9_]+)\],\s*Source=\{x:Static\s+[a-zA-Z0-9_]+:LocalizationResourceManager\.Instance\}\}', '{extensions:Translate ${1}}')
    
    if ($c -cne $orig) {
        if ($c -notmatch 'xmlns:extensions') {
            $c = $c -replace '<ContentPage ', "<ContentPage xmlns:extensions=`"clr-namespace:KamPay.Extensions`"`r`n             "
        }
        Set-Content -Path $f.FullName -Value $c
        Write-Host "Updated $($f.Name)"
    }
}
