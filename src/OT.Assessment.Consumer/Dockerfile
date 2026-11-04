# Build from the repository root: docker build -f src/OT.Assessment.Consumer/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY src/OT.Assessment.Consumer/OT.Assessment.Consumer.csproj src/OT.Assessment.Consumer/
COPY src/OT.Assessment.Domain/OT.Assessment.Domain.csproj src/OT.Assessment.Domain/
COPY src/OT.Assessment.Infrastructure/OT.Assessment.Infrastructure.csproj src/OT.Assessment.Infrastructure/
RUN dotnet restore src/OT.Assessment.Consumer/OT.Assessment.Consumer.csproj

COPY src/ src/
RUN dotnet publish src/OT.Assessment.Consumer/OT.Assessment.Consumer.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "OT.Assessment.Consumer.dll"]
