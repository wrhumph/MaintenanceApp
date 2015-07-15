if ([Environment]::Is64BitProcess)
{
    Write-Host "This is a 64-bit process"
}
else
{
    Write-Host "This is a 32-bit process"
}
Write-Host "-------------------"

get-acl .

Write-Host " "
Write-Host " "
Write-Host -NoNewline "Press any key to continue ..."
$x = $host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
