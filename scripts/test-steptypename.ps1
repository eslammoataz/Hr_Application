$base = "http://localhost:5119"
$login = Invoke-RestMethod -Method POST -Uri "$base/api/auth/login" -ContentType "application/json" -Body (@{ email="superadmin@hrms.com"; password="SuperAdmin@123" } | ConvertTo-Json) -ErrorAction SilentlyContinue
$token = $login.data.token
$headers = @{ "Content-Type" = "application/json"; "Authorization" = "Bearer $token" }

$r = Invoke-RestMethod -Method GET -Uri "$base/api/requestdefinitions?companyId=58ecb31e-7b4b-4209-b0d3-87f903d5b2cc" -ContentType "application/json" -Headers $headers -ErrorAction SilentlyContinue

Write-Host "=== stepTypeName in response ==="
foreach ($item in $r.data[0..2]) {
    Write-Host "Type $($item.requestType) $($item.requestName):"
    foreach ($s in $item.steps) {
        Write-Host "  sortOrder=$($s.sortOrder) stepType=$($s.stepType) stepTypeName=$($s.stepTypeName)"
    }
    Write-Host ""
}