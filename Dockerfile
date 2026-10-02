# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from project files only, so the layer is cached until dependencies change.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/ErpMcp.Domain/ErpMcp.Domain.csproj src/ErpMcp.Domain/
COPY src/ErpMcp.Application/ErpMcp.Application.csproj src/ErpMcp.Application/
COPY src/ErpMcp.Infrastructure/ErpMcp.Infrastructure.csproj src/ErpMcp.Infrastructure/
COPY src/ErpMcp.Server/ErpMcp.Server.csproj src/ErpMcp.Server/
RUN dotnet restore src/ErpMcp.Server/ErpMcp.Server.csproj

COPY src/ src/
RUN dotnet publish src/ErpMcp.Server/ErpMcp.Server.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Run as the image's built-in unprivileged user.
USER $APP_UID

# MCP over stdio: start with `docker run -i`. Extra args select CLI commands (migrate, orders, audit).
ENTRYPOINT ["dotnet", "erp-mcp.dll"]
