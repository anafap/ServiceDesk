FROM node:24-bookworm-slim AS frontend
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY ServiceDesk.Domain/ServiceDesk.Domain.csproj ServiceDesk.Domain/
COPY ServiceDesk.Application/ServiceDesk.Application.csproj ServiceDesk.Application/
COPY ServiceDesk.Infrastructure/ServiceDesk.Infrastructure.csproj ServiceDesk.Infrastructure/
COPY ServiceDesk.Api/ServiceDesk.Api.csproj ServiceDesk.Api/
RUN dotnet restore ServiceDesk.Api/ServiceDesk.Api.csproj
COPY ServiceDesk.Domain/ ServiceDesk.Domain/
COPY ServiceDesk.Application/ ServiceDesk.Application/
COPY ServiceDesk.Infrastructure/ ServiceDesk.Infrastructure/
COPY ServiceDesk.Api/ ServiceDesk.Api/
RUN dotnet publish ServiceDesk.Api -c Release --no-restore -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble
WORKDIR /app
COPY --from=backend /out/ ./
COPY --from=frontend /src/frontend/dist/ ./wwwroot/
RUN mkdir -p /data/photos && chown -R $APP_UID:$APP_UID /data/photos
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080 ASPNETCORE_ENVIRONMENT=Production Photos__Root=/data/photos
EXPOSE 8080
ENTRYPOINT ["dotnet", "ServiceDesk.Api.dll"]
