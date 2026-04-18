$base = "http://localhost:5119"
$body = @{
    email = "superadmin@hrms.com"
    password = "SuperAdmin@123"
} | ConvertTo-Json

$login = Invoke-RestMethod -Method POST -Uri "$base/api/auth/login" -ContentType "application/json" -Body $body -ErrorAction SilentlyContinue
$token = $login.data.token

$headers = @{
    "Content-Type" = "application/json"
    "Authorization" = "Bearer $token"
}

# Try to create employee using same approach as script
$bodyHash = @{
    fullName = "Test Emp XYZ"
    email = "testemp_xyz_new@demo.com"
    phoneNumber = "99999999991"
    companyId = "c0b0b399-7198-4232-9f80-669b827d2e82"
    role = 4
}

$jsonBody = $bodyHash | ConvertTo-Json -Depth 10
Write-Host "JSON Body: $jsonBody"

try {
    $r = Invoke-RestMethod -Method POST -Uri "$base/api/employees" -ContentType "application/json" -Headers $headers -Body $jsonBody
    Write-Host "Success:"
    $r | ConvertTo-Json -Depth 10
} catch {
    Write-Host "Error caught: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $stream = $_.Exception.Response.GetResponseStream()
        $reader = [System.IO.StreamReader]::new($stream)
        $errorBody = $reader.ReadToEnd()
        $reader.Close()
        Write-Host "Response body: $errorBody"
    }
}