FROM mcr.microsoft.com/dotnet/sdk:10.0.102@sha256:6a135580d677983f9bf4094fe1918a64e80536b9544e6c68160f6a515a5312f0 AS build
WORKDIR /source
COPY global.json Directory.Build.props Directory.Packages.props NuGet.Config ./
COPY src/ src/
COPY aspire/HaXus.ServiceDefaults/ aspire/HaXus.ServiceDefaults/
RUN dotnet restore src/HaXus.Ugnay/HaXus.Ugnay.csproj --locked-mode
RUN dotnet publish src/HaXus.Ugnay/HaXus.Ugnay.csproj -c Release --no-restore -o /app
FROM mcr.microsoft.com/dotnet/aspnet:10.0.2@sha256:242b8429bdef9731e6c05931a7caec0dedf9e94462d8f6d12fc47ba03f0d3692
WORKDIR /app
COPY --from=build /app ./
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "HaXus.Ugnay.dll"]
