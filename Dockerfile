FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/TreeEditor.Contracts/TreeEditor.Contracts.csproj src/TreeEditor.Contracts/
COPY src/TreeEditor.Client/TreeEditor.Client.csproj src/TreeEditor.Client/
COPY src/TreeEditor.Api/TreeEditor.Api.csproj src/TreeEditor.Api/
RUN dotnet restore src/TreeEditor.Api/TreeEditor.Api.csproj

COPY src/ src/
RUN dotnet publish src/TreeEditor.Api/TreeEditor.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "TreeEditor.Api.dll"]
