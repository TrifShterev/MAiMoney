# Build from the repository root:  docker build -f src/MAiMoney.Web/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached until a project file changes. (Publish restores again once the
# source is present: the Blazor script package is only resolved then, and without it the buttons never work.)
COPY src/MAiMoney.Services/MAiMoney.Services.csproj src/MAiMoney.Services/
COPY src/MAiMoney.Web/MAiMoney.Web.csproj src/MAiMoney.Web/
RUN dotnet restore src/MAiMoney.Web/MAiMoney.Web.csproj

COPY src/ src/
RUN dotnet publish src/MAiMoney.Web/MAiMoney.Web.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Data Protection keys are kept on a volume mounted here; the app user must own it.
RUN mkdir /keys && chown app:app /keys

COPY --from=build /app/publish .

USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "MAiMoney.Web.dll"]
