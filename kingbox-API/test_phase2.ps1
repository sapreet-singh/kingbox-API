$baseUrl = 'http://localhost:5225'

Write-Host "=== 1. Health Check ==="
$health = Invoke-RestMethod -Uri "$baseUrl/api/health" -Method Get
Write-Host ($health | ConvertTo-Json)

Write-Host "`n=== 2. Tools Status ==="
$tools = Invoke-RestMethod -Uri "$baseUrl/api/media/tools/status" -Method Get
Write-Host ($tools | ConvertTo-Json -Depth 5)

Write-Host "`n=== 3. Media Info (Invalid URL - expect 400) ==="
try {
    Invoke-RestMethod -Uri "$baseUrl/api/media/info" -Method Post -Body (@{ url = 'not-a-valid-url' } | ConvertTo-Json) -ContentType 'application/json'
} catch {
    Write-Host "Caught expected 400: $($_.Exception.Message)"
}

Write-Host "`n=== 4. Media Convert (Valid Request) ==="
$convReq = @{ url = 'https://example.com/sample-media'; format = 'mp3'; quality = '320' } | ConvertTo-Json
$convRes = Invoke-RestMethod -Uri "$baseUrl/api/media/convert" -Method Post -Body $convReq -ContentType 'application/json'
Write-Host ($convRes | ConvertTo-Json)
$jobId = $convRes.conversionId

Write-Host "`n=== 5. Media Convert (Invalid Format - expect 400) ==="
try {
    Invoke-RestMethod -Uri "$baseUrl/api/media/convert" -Method Post -Body (@{ url = 'https://example.com/v'; format = 'mkv'; quality = '320' } | ConvertTo-Json) -ContentType 'application/json'
} catch {
    Write-Host "Caught expected 400: $($_.Exception.Message)"
}

Write-Host "`n=== 6. Media Convert (Invalid Quality - expect 400) ==="
try {
    Invoke-RestMethod -Uri "$baseUrl/api/media/convert" -Method Post -Body (@{ url = 'https://example.com/v'; format = 'mp3'; quality = '500' } | ConvertTo-Json) -ContentType 'application/json'
} catch {
    Write-Host "Caught expected 400: $($_.Exception.Message)"
}

Write-Host "`n=== 7. Progress Endpoint (Valid Job ID) ==="
$progress = Invoke-RestMethod -Uri "$baseUrl/api/media/progress/$jobId" -Method Get
Write-Host ($progress | ConvertTo-Json)

Write-Host "`n=== 8. Progress Endpoint (Unknown ID - expect 404) ==="
try {
    Invoke-RestMethod -Uri "$baseUrl/api/media/progress/00000000-0000-0000-0000-000000000000" -Method Get
} catch {
    Write-Host "Caught expected 404: $($_.Exception.Message)"
}

Write-Host "`n=== 9. Cancel Job ==="
$cancel = Invoke-RestMethod -Uri "$baseUrl/api/media/cancel/$jobId" -Method Post
Write-Host ($cancel | ConvertTo-Json)

Write-Host "`n=== 10. Download Incomplete Job (expect 409) ==="
try {
    Invoke-RestMethod -Uri "$baseUrl/api/media/download/$jobId" -Method Get
} catch {
    Write-Host "Caught expected 409: $($_.Exception.Message)"
}

Write-Host "`n=== 11. Download Unknown Job (expect 404) ==="
try {
    Invoke-RestMethod -Uri "$baseUrl/api/media/download/00000000-0000-0000-0000-000000000000" -Method Get
} catch {
    Write-Host "Caught expected 404: $($_.Exception.Message)"
}

Write-Host "`n=== 12. Swagger OpenAPI Specification ==="
$swagger = Invoke-RestMethod -Uri "$baseUrl/swagger/v1/swagger.json" -Method Get
Write-Host "Swagger Title: $($swagger.info.title), Version: $($swagger.info.version)"
Write-Host "Endpoints found in Swagger: $($swagger.paths.PSObject.Properties.Name -join ', ')"
