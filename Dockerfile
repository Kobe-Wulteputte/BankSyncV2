# ---- client ----
FROM node:22-alpine AS client
WORKDIR /client
COPY client/package*.json ./
RUN npm ci
COPY client/ ./
# Vite writes to ../src/BS2.Api/wwwroot; override to a local folder inside this stage.
RUN npx vite build --outDir /client/dist --emptyOutDir

# ---- api ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/BS2.Domain/BS2.Domain.csproj src/BS2.Domain/
COPY src/BS2.Application/BS2.Application.csproj src/BS2.Application/
COPY src/BS2.Infrastructure/BS2.Infrastructure.csproj src/BS2.Infrastructure/
COPY src/BS2.Api/BS2.Api.csproj src/BS2.Api/
RUN dotnet restore src/BS2.Api/BS2.Api.csproj
COPY src/ src/
RUN dotnet publish src/BS2.Api/BS2.Api.csproj -c Release -o /app/publish --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish ./
COPY --from=client /client/dist ./wwwroot
EXPOSE 8080
ENTRYPOINT ["dotnet", "BS2.Api.dll"]
