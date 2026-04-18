$base = "http://localhost:5119"
$body = @{
    email = "superadmin@hrms.com"
    password = "SuperAdmin@123"
} | ConvertTo-Json

$login = Invoke-RestMethod -Method POST -Uri "$base/api/auth/login" -ContentType "application/json" -Body $body
$token = $login.data.token

$headers = @{
    "Content-Type" = "application/json"
    "Authorization" = "Bearer $token"
}

$nodeBody = @{
    name = "TestDiv3"
    type = "Division"
    parentId = $null
    companyId = "37a59a56-acbf-41ab-87dd-12837847c853"
} | ConvertTo-Json

Write-Host "Creating orgnode..."
$r = Invoke-RestMethod -Method POST -Uri "$base/api/orgnodes" -ContentType "application/json" -Headers $headers -Body $nodeBody -ErrorAction SilentlyContinue
Write-Host "Response:"
$r | ConvertTo-Json -Depth 10

Write-Host ""
Write-Host "Now getting company info..."
$cos = Invoke-RestMethod -Method GET -Uri "$base/api/companies" -ContentType "application/json" -Headers $headers -ErrorAction SilentlyContinue
$cos.data.items | ConvertTo-Json -Depth 5