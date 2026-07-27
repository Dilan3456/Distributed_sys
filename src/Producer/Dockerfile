# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Shared/Shared.csproj Shared/
COPY Producer/Producer.csproj Producer/
RUN dotnet restore Producer/Producer.csproj
COPY Shared/ Shared/
COPY Producer/ Producer/
RUN dotnet publish Producer/Producer.csproj -c Release -o /app

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Producer.dll"]
