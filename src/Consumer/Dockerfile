# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Shared/Shared.csproj Shared/
COPY Consumer/Consumer.csproj Consumer/
RUN dotnet restore Consumer/Consumer.csproj
COPY Shared/ Shared/
COPY Consumer/ Consumer/
RUN dotnet publish Consumer/Consumer.csproj -c Release -o /app

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Consumer.dll"]
