# Build and run the Northline TMS API.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Northline.Api/Northline.Api.csproj src/Northline.Api/
RUN dotnet restore src/Northline.Api/Northline.Api.csproj
COPY src/ src/
RUN dotnet publish src/Northline.Api/Northline.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Northline.Api.dll"]
