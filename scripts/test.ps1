dotnet test --collect="XPlat Code Coverage";

reportgenerator -reports:tests\**\*.xml -targetdir:report/

./report/index.html

