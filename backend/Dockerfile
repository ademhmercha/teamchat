# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restauration separee (mise en cache des dependances).
# On restaure uniquement le projet API (et ses references) : la solution
# reference aussi les projets de tests, absents de l'image a ce stade.
COPY src/TeamChat.Domain/TeamChat.Domain.csproj src/TeamChat.Domain/
COPY src/TeamChat.Application/TeamChat.Application.csproj src/TeamChat.Application/
COPY src/TeamChat.Infrastructure/TeamChat.Infrastructure.csproj src/TeamChat.Infrastructure/
COPY src/TeamChat.Api/TeamChat.Api.csproj src/TeamChat.Api/
RUN dotnet restore src/TeamChat.Api/TeamChat.Api.csproj

COPY . .
RUN dotnet publish src/TeamChat.Api/TeamChat.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TeamChat.Api.dll"]
