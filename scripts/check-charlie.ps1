$base = "http://localhost:5119"
$login = Invoke-RestMethod -Method POST -Uri "$base/api/auth/login" -ContentType "application/json" -Body (@{ email="superadmin@hrms.com"; password="SuperAdmin@123" } | ConvertTo-Json) -ErrorAction SilentlyContinue
$token = $login.data.token
$headers = @{ "Content-Type" = "application/json"; "Authorization" = "Bearer $token" }

$charlieId = "bad06fc1-9ce1-4622-9cdb-c38549614b1f"

Write-Host "=== Charlie Employee ==="
$emp = Invoke-RestMethod -Method GET -Uri "$base/api/employees/$charlieId" -ContentType "application/json" -Headers $headers -ErrorAction SilentlyContinue
$emp | ConvertTo-Json -Depth 5

Write-Host ""
Write-Host "=== Charlie Leave Balances ==="
$bal = Invoke-RestMethod -Method GET -Uri "$base/api/leave-balances?employeeId=$charlieId" -ContentType "application/json" -Headers $headers -ErrorAction SilentlyContinue
$bal | ConvertTo-Json -Depth 5