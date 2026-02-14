# Build stage for React SPA
FROM node:20-alpine AS frontend-build
WORKDIR /app/frontend

# Copy frontend files
COPY src/Log4YM.Web/package*.json ./
RUN npm ci

COPY src/Log4YM.Web/ ./
RUN npm run build

# Build stage for .NET
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /app

# Copy csproj files and restore
COPY Log4YM.sln ./
COPY src/Log4YM.Server/Log4YM.Server.csproj ./src/Log4YM.Server/
COPY src/Log4YM.Contracts/Log4YM.Contracts.csproj ./src/Log4YM.Contracts/
RUN dotnet restore

# Copy source and build
COPY src/ ./src/
RUN dotnet publish src/Log4YM.Server/Log4YM.Server.csproj -c Release -o /app/publish

# Copy frontend build to wwwroot
COPY --from=frontend-build /app/frontend/dist /app/publish/wwwroot

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# Install icu-libs for globalization support
RUN apk add --no-cache icu-libs

# Set environment
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV ASPNETCORE_URLS=http://+:5050
ENV ASPNETCORE_ENVIRONMENT=Production

# Copy published app
COPY --from=backend-build /app/publish .

# Create non-root user and config directory
RUN addgroup -S log4ym && adduser -S log4ym -G log4ym \
    && mkdir -p /home/log4ym/.config/Log4YM \
    && chown -R log4ym:log4ym /home/log4ym
USER log4ym

EXPOSE 5050

ENTRYPOINT ["dotnet", "Log4YM.Server.dll"]
