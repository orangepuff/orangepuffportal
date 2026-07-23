FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["OrangepuffPortal.slnx", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["NuGet.config", "./"]
COPY ["samples/OrangepuffPortal.SampleHost/OrangepuffPortal.SampleHost.csproj", "samples/OrangepuffPortal.SampleHost/"]
COPY ["src/OrangepuffPortal.Host/OrangepuffPortal.Host.csproj", "src/OrangepuffPortal.Host/"]
COPY ["src/OrangepuffPortal.Bff/OrangepuffPortal.Bff.csproj", "src/OrangepuffPortal.Bff/"]
COPY ["src/OrangepuffPortal.Identity/OrangepuffPortal.Identity.csproj", "src/OrangepuffPortal.Identity/"]
COPY ["src/OrangepuffPortal.Identity.Contract/OrangepuffPortal.Identity.Contract.csproj", "src/OrangepuffPortal.Identity.Contract/"]
COPY ["src/OrangepuffPortal.Shared/OrangepuffPortal.Shared.csproj", "src/OrangepuffPortal.Shared/"]
RUN dotnet restore "samples/OrangepuffPortal.SampleHost/OrangepuffPortal.SampleHost.csproj"

COPY . .
RUN dotnet publish "samples/OrangepuffPortal.SampleHost/OrangepuffPortal.SampleHost.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8081
ENV ASPNETCORE_URLS=https://+:8081
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OrangepuffPortal.SampleHost.dll"]
