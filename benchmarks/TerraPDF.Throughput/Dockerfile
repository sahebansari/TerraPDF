# Throughput harness in a container. Build context: repository root.
#   docker build -f benchmarks/TerraPDF.Throughput/Dockerfile -t terrapdf-throughput .
#   docker run --rm --cpus=2 --memory=1g terrapdf-throughput --seconds 30
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish benchmarks/TerraPDF.Throughput/TerraPDF.Throughput.csproj -c Release -o /app \
      -p:GeneratePackageOnBuild=false -p:EnableSourceLink=false -p:ContinuousIntegrationBuild=false

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "TerraPDF.Throughput.dll"]
