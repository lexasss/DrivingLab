# Get-AuthenticodeSignature "C:\Windows\System32\DriverStore\FileRepository\smired.inf_amd64_27767c27e39b5c5a\SMI_NTx64.cat" |
# Format-List *

# Remove driver check with expired certificates
CiTool.exe --remove-policy "{8F9CB695-5D48-48D6-A329-7202B44607E3}"