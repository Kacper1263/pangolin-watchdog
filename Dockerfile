# docker build -t pangolin-watchdog:latest .
# (docker tag pangolin-watchdog:latest kacper1263/pangolin-watchdog:v1.0)
# (docker push kacper1263/pangolin-watchdog:v1.0)
# docker save -o watchdog_image.tar pangolin-watchdog:latest
#
# docker load -i watchdog_image.tar
# docker-compose up -d

# base
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
RUN mkdir -p /app/data
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

# build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["PangolinWatchdog.csproj", "./"]
RUN dotnet restore "PangolinWatchdog.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "PangolinWatchdog.csproj" -c $BUILD_CONFIGURATION -o /app/build

# publish
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "PangolinWatchdog.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# final
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "PangolinWatchdog.dll"]