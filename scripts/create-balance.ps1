$base = "http://localhost:5119"
$superAdminToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhdWQiOiJIclN5c3RlbUFwcCIsImlzcyI6IkhyU3lzdGVtQXBwIiwiZXhwIjoxNzc2NTI3NzUyLCJqdGkiOiJlNTBjNzUyMy04MTc1LTQwNDQtYjljZS1mMDM5ODdiODAzNTUiLCJzdWIiOiIzZDI0YTNkOC1hMGQyLTQ2ZDktYmNjMy0wMGFkYmZjYTQ5MGQiLCJlbWFpbCI6ImFkbWluX2RlbW9AZGVtby5jb20iLCJuYW1lIjoiRGVtb0NvIEFkbWluIiwicm9sZSI6IkNvbXBhbnlBZG1pbiIsInBob25lIjoiOTkxMjM0NTY3ODkiLCJjb21wYW55SWQiOiIiLCJlbXBsb3llZUlkIjoiNjYyMjM0ZDItZTkzMy00ZDliLTg1OTItNWUyMGJhOGY0ZDU3IiwiaWF0IjoxNzc2NTI0MTUyLCJuYmYiOjE3NzY1MjQxNTJ9.Q3gLPuhsV_ZVDtoLpMr9kRkx1FtgclHyc6gB2QnbYOI"

$headers = @{
    "Content-Type" = "application/json"
    "Authorization" = "Bearer $superAdminToken"
}

$body = @{
    leaveType = 0
    year = 2026
    totalDays = 30
} | ConvertTo-Json

Write-Host "Creating leave balance for Charlie..."
$r = Invoke-RestMethod -Method PUT -Uri "$base/api/admin/employees/bad06fc1-9ce1-4622-9cdb-c38549614b1f/leave-balances" -ContentType "application/json" -Headers $headers -Body $body -ErrorAction SilentlyContinue
if ($r) {
    $r | ConvertTo-Json -Depth 5
} else {
    Write-Host "Response was empty"
}