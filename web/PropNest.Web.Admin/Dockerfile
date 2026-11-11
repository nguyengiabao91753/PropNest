# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy Directory-level configuration for Central Package Management & build props
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]

# Copy project files for caching dependency restore
COPY ["src/PropNest.Domain/PropNest.Domain.csproj", "src/PropNest.Domain/"]
COPY ["web/PropNest.Web.Admin/PropNest.Web.Admin.csproj", "web/PropNest.Web.Admin/"]

# Restore dependencies
RUN dotnet restore "web/PropNest.Web.Admin/PropNest.Web.Admin.csproj"

# Copy source code
COPY ["src/PropNest.Domain/", "src/PropNest.Domain/"]
COPY ["web/PropNest.Web.Admin/", "web/PropNest.Web.Admin/"]

# Build and Publish
WORKDIR "/src/web/PropNest.Web.Admin"
RUN dotnet publish "PropNest.Web.Admin.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PropNest.Web.Admin.dll"]
