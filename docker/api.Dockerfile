# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, using only project files, so this layer is cached until dependencies change.
COPY global.json Directory.Build.props ./
COPY src/BranchBooking.Domain/BranchBooking.Domain.csproj               src/BranchBooking.Domain/
COPY src/BranchBooking.Application/BranchBooking.Application.csproj     src/BranchBooking.Application/
COPY src/BranchBooking.Infrastructure/BranchBooking.Infrastructure.csproj src/BranchBooking.Infrastructure/
COPY src/BranchBooking.Api/BranchBooking.Api.csproj                     src/BranchBooking.Api/
RUN dotnet restore src/BranchBooking.Api/BranchBooking.Api.csproj

COPY src/ src/
ARG VERSION=0.0.0-dev
RUN dotnet publish src/BranchBooking.Api/BranchBooking.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false /p:Version=${VERSION}

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "BranchBooking.Api.dll"]
