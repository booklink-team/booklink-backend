FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY global.json Booklink.sln ./
COPY src/Booklink.Domain/*.csproj src/Booklink.Domain/
COPY src/Booklink.Application/*.csproj src/Booklink.Application/
COPY src/Booklink.Infrastructure/*.csproj src/Booklink.Infrastructure/
COPY src/Booklink.Api/*.csproj src/Booklink.Api/
RUN dotnet restore src/Booklink.Api
COPY src/ src/
RUN dotnet publish src/Booklink.Api -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Booklink.Api.dll"]
