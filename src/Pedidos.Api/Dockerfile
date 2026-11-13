# Build context: raiz do repositório (ver docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore em camada separada: só refaz quando algum .csproj muda.
COPY global.json Directory.Build.props ./
COPY src/Pedidos.Domain/Pedidos.Domain.csproj src/Pedidos.Domain/
COPY src/Pedidos.Application/Pedidos.Application.csproj src/Pedidos.Application/
COPY src/Pedidos.Infrastructure/Pedidos.Infrastructure.csproj src/Pedidos.Infrastructure/
COPY src/Pedidos.Api/Pedidos.Api.csproj src/Pedidos.Api/
RUN dotnet restore src/Pedidos.Api/Pedidos.Api.csproj

COPY src/ src/
RUN dotnet publish src/Pedidos.Api/Pedidos.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Pedidos.Api.dll"]
