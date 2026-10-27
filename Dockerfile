FROM mcr.microsoft.com/dotnet/sdk:11.0.100-rc.2 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY BirthdayBot/BirthdayBot.csproj BirthdayBot/
RUN dotnet restore BirthdayBot/BirthdayBot.csproj
COPY BirthdayBot/ BirthdayBot/
RUN dotnet publish BirthdayBot/BirthdayBot.csproj -c Release -o /app/publish -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:11.0.0-rc.2
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BirthdayBot.dll"]
