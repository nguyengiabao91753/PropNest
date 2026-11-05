# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy Directory-level configuration for Central Package Management & build props
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]

# Copy project files for caching dependency restore
COPY ["src/PropNest.Domain/PropNest.Domain.csproj", "src/PropNest.Domain/"]
COPY ["src/PropNest.Application/PropNest.Application.csproj", "src/PropNest.Application/"]
COPY ["src/PropNest.Infrastructure/PropNest.Infrastructure.csproj", "src/PropNest.Infrastructure/"]
COPY ["src/PropNest.Workflow.Worker/PropNest.Workflow.Worker.csproj", "src/PropNest.Workflow.Worker/"]

# Restore dependencies
RUN dotnet restore "src/PropNest.Workflow.Worker/PropNest.Workflow.Worker.csproj"

# Copy source code
COPY ["src/PropNest.Domain/", "src/PropNest.Domain/"]
COPY ["src/PropNest.Application/", "src/PropNest.Application/"]
COPY ["src/PropNest.Infrastructure/", "src/PropNest.Infrastructure/"]
COPY ["src/PropNest.Workflow.Worker/", "src/PropNest.Workflow.Worker/"]

# Build and Publish
WORKDIR "/src/src/PropNest.Workflow.Worker"
RUN dotnet publish "PropNest.Workflow.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PropNest.Workflow.Worker.dll"]
