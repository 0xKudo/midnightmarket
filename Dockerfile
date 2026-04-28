FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json .
COPY ArmsFair.sln .
COPY ArmsFair.Shared/ArmsFair.Shared.csproj        ArmsFair.Shared/
COPY ArmsFair.Server/ArmsFair.Server.csproj        ArmsFair.Server/
COPY ArmsFair.Server.Tests/ArmsFair.Server.Tests.csproj ArmsFair.Server.Tests/

RUN dotnet restore ArmsFair.Server/ArmsFair.Server.csproj

COPY ArmsFair.Shared/   ArmsFair.Shared/
COPY ArmsFair.Server/   ArmsFair.Server/

RUN dotnet publish ArmsFair.Server/ArmsFair.Server.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN addgroup --system armsfair && adduser --system --ingroup armsfair armsfair
USER armsfair

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ArmsFair.Server.dll"]
