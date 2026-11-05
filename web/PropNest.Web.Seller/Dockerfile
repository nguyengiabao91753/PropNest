# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy Directory-level configuration for Central Package Management & build props
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]

# Copy project files for caching dependency restore
COPY ["src/PropNest.Domain/PropNest.Domain.csproj", "src/PropNest.Domain/"]
COPY ["web/PropNest.Web.Seller/PropNest.Web.Seller.csproj", "web/PropNest.Web.Seller/"]

# Restore dependencies
RUN dotnet restore "web/PropNest.Web.Seller/PropNest.Web.Seller.csproj"

# Copy source code
COPY ["src/PropNest.Domain/", "src/PropNest.Domain/"]
COPY ["web/PropNest.Web.Seller/", "web/PropNest.Web.Seller/"]

# Build and Publish
WORKDIR "/src/web/PropNest.Web.Seller"
RUN dotnet publish "PropNest.Web.Seller.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PropNest.Web.Seller.dll"]
