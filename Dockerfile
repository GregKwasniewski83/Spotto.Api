# Base runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 7125
RUN apt-get update && apt-get install -y ca-certificates

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the whole solution (all projects + .sln)
COPY . .

# Restore and publish the API project
WORKDIR /src/PlaySpace.Api
RUN dotnet restore "PlaySpace.Api.csproj"
RUN dotnet publish "PlaySpace.Api.csproj" -c Release -o /app/publish

# Final runtime image
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:7125
ENTRYPOINT ["dotnet", "PlaySpace.Api.dll"]
