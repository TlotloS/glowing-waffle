# Build from the repository root: docker build -f test/OT.Assessment.Tester/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY test/OT.Assessment.Tester/OT.Assessment.Tester.csproj test/OT.Assessment.Tester/
RUN dotnet restore test/OT.Assessment.Tester/OT.Assessment.Tester.csproj

COPY test/OT.Assessment.Tester/ test/OT.Assessment.Tester/
RUN dotnet publish test/OT.Assessment.Tester/OT.Assessment.Tester.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "OT.Assessment.Tester.dll"]
