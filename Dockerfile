FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/HackerNewsBestStories.Api/HackerNewsBestStories.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:5138
EXPOSE 5138
ENTRYPOINT ["dotnet", "HackerNewsBestStories.Api.dll"]