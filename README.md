# OAuthSolution


## Before run the solution, create OAuth2 api credentials on github platform, and configure secure config within the solution running the commands:
```powershell
dotnet user-secrets init
dotnet user-secrets set "Authentication:Github:ClientId" "<cliendid>"
dotnet user-secrets set "Authentication:Github:ClientSecret" "<clientsecret>"
```
