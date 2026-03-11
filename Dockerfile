FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files
COPY ["HrSystemApp.Domain/HrSystemApp.Domain.csproj", "HrSystemApp.Domain/"]
COPY ["HrSystemApp.Application/HrSystemApp.Application.csproj", "HrSystemApp.Application/"]
COPY ["HrSystemApp.Infrastructure/HrSystemApp.Infrastructure.csproj", "HrSystemApp.Infrastructure/"]
COPY ["HrSystemApp.Api/HrSystemApp.Api.csproj", "HrSystemApp.Api/"]

# Restore
RUN dotnet restore "HrSystemApp.Api/HrSystemApp.Api.csproj"

# Copy everything and build
COPY . .
WORKDIR "/src/HrSystemApp.Api"
RUN dotnet build -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HrSystemApp.Api.dll"]
