FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY Directory.Build.props ./
COPY src/CachedQueries/CachedQueries.csproj src/CachedQueries/
COPY examples/Demo.Api/Demo.Api.csproj examples/Demo.Api/
RUN dotnet restore examples/Demo.Api/Demo.Api.csproj

# Copy everything and build
COPY src/CachedQueries/ src/CachedQueries/
COPY examples/Demo.Api/ examples/Demo.Api/
RUN dotnet publish examples/Demo.Api/Demo.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Demo.Api.dll"]
