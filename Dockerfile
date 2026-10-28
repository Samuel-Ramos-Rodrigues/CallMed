FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 10000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["CallMedCrud/CallMedCrud.csproj", "CallMedCrud/"]
RUN dotnet restore "CallMedCrud/CallMedCrud.csproj"

COPY ["CallMedCrud/", "CallMedCrud/"]
WORKDIR "/src/CallMedCrud"
RUN dotnet publish "CallMedCrud.csproj" -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:10000
ENV DOTNET_USE_POLLING_FILE_WATCHER=1
ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
ENTRYPOINT ["dotnet", "CallMedCrud.dll"]
