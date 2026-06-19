# syntax=docker/dockerfile:1

# ── build ──────────────────────────────────────────────────────────────
# .NET 10 이미지는 멀티아치(arm64 포함)라 Oracle Ampere(ARM) VM 에서 그대로 빌드된다.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 먼저 csproj 만 복사해 restore 레이어를 캐시한다.
COPY src/GymHub.Server/GymHub.Server.csproj src/GymHub.Server/
RUN dotnet restore src/GymHub.Server/GymHub.Server.csproj

COPY src/ src/
RUN dotnet publish src/GymHub.Server/GymHub.Server.csproj -c Release -o /app /p:UseAppHost=false

# ── runtime ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# 컨테이너는 8080 평문으로 listen 한다(TLS 는 앞단 프록시가 종료).
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "GymHub.Server.dll"]
