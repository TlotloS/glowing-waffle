# Build context is the repository root, so the App can see Core and Infrastructure.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/OT.Assessment.App/OT.Assessment.App.csproj", "src/OT.Assessment.App/"]
COPY ["src/OT.Assessment.Core/OT.Assessment.Core.csproj", "src/OT.Assessment.Core/"]
COPY ["src/OT.Assessment.Infrastructure/OT.Assessment.Infrastructure.csproj", "src/OT.Assessment.Infrastructure/"]
RUN dotnet restore "src/OT.Assessment.App/OT.Assessment.App.csproj"
COPY src/ src/
RUN dotnet publish "src/OT.Assessment.App/OT.Assessment.App.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OT.Assessment.App.dll"]
