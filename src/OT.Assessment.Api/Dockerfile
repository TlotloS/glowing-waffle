# Build from the repository root: docker build -f src/OT.Assessment.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY src/OT.Assessment.Api/OT.Assessment.Api.csproj src/OT.Assessment.Api/
COPY src/OT.Assessment.Domain/OT.Assessment.Domain.csproj src/OT.Assessment.Domain/
COPY src/OT.Assessment.Infrastructure/OT.Assessment.Infrastructure.csproj src/OT.Assessment.Infrastructure/
RUN dotnet restore src/OT.Assessment.Api/OT.Assessment.Api.csproj

COPY src/ src/
RUN dotnet publish src/OT.Assessment.Api/OT.Assessment.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "OT.Assessment.Api.dll"]
