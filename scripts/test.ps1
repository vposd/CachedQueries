dotnet test --collect="XPlat Code Coverage";

./tools/reportgenerator.exe -reports:tests\**\*.xml -targetdir:report/

