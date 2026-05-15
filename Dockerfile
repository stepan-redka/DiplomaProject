# BUILD STAGE
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /app

# Copy csproj files and restore as distinct layers
COPY ["Diploma.Web/Diploma.Web.csproj", "Diploma.Web/"]
COPY ["Diploma.Application/Diploma.Application.csproj", "Diploma.Application/"]
COPY ["Diploma.Infrastructure/Diploma.Infrastructure.csproj", "Diploma.Infrastructure/"]
COPY ["Diploma.Domain/Diploma.Domain.csproj", "Diploma.Domain/"]
RUN dotnet restore "Diploma.Web/Diploma.Web.csproj"

# Copy everything else and build app
COPY . .
WORKDIR /app/Diploma.Web
RUN dotnet publish -c Release -o /out

# RUN STAGE
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app
COPY --from=build /out .

# Use a non-root user for security
RUN adduser -u 1000 -D appuser
USER appuser

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Diploma.Web.dll"]
