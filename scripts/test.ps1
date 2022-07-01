dotnet test --collect="XPlat Code Coverage";

reportgenerator -reports:tests\**\*.xml -targetdir:report/

./report/index.html

Get-ChildItem tests/**/TestResults | Remove-Item -Recurse -Force
