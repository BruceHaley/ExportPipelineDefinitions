# Stage 1: https://hub.docker.com/_/microsoft-dotnet
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
ENV outputPath=/output/data/
WORKDIR /app
ENTRYPOINT ["dotnet", "ExportPipelineDefinitions.dll"]

# Disable .NET diagnostics so app does no need write permissions to /tmp
ENV COMPlus_EnableDiagnostics=0


# Stage 2: Build application with SDK
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /source

# copy csproj and restore as distinct layers
COPY *.sln .
COPY ExportPipelineDefinitions/*.csproj ./ExportPipelineDefinitions/
RUN dotnet restore

# copy everything else and build app
COPY ExportPipelineDefinitions/. ./ExportPipelineDefinitions/
WORKDIR /source/ExportPipelineDefinitions
RUN dotnet publish -o /app --no-restore


# Stage 3: Copy binaries to hardened runtime image
FROM base AS final
COPY --from=build /app ./
