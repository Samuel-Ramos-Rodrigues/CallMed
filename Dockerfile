FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/CallMed.Domain/CallMed.Domain.csproj src/CallMed.Domain/
COPY src/CallMed.Persistence/CallMed.Persistence.csproj src/CallMed.Persistence/
COPY src/CallMed.Application/CallMed.Application.csproj src/CallMed.Application/
COPY CallMedCrud/CallMedCrud.csproj CallMedCrud/
RUN dotnet restore CallMedCrud/CallMedCrud.csproj
COPY src/ src/
COPY CallMedCrud/ CallMedCrud/
RUN dotnet publish CallMedCrud/CallMedCrud.csproj -c Release --no-restore -o /publish /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 10000
COPY --from=build /publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
ENV DOTNET_USE_POLLING_FILE_WATCHER=1
ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
ENTRYPOINT ["dotnet", "CallMedCrud.dll"]
