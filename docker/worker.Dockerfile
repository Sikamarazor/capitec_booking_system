# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props ./
COPY src/BranchBooking.Domain/BranchBooking.Domain.csproj               src/BranchBooking.Domain/
COPY src/BranchBooking.Application/BranchBooking.Application.csproj     src/BranchBooking.Application/
COPY src/BranchBooking.Infrastructure/BranchBooking.Infrastructure.csproj src/BranchBooking.Infrastructure/
COPY src/BranchBooking.Worker/BranchBooking.Worker.csproj               src/BranchBooking.Worker/
RUN dotnet restore src/BranchBooking.Worker/BranchBooking.Worker.csproj

COPY src/ src/
ARG VERSION=0.0.0-dev
RUN dotnet publish src/BranchBooking.Worker/BranchBooking.Worker.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false /p:Version=${VERSION}

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "BranchBooking.Worker.dll"]
