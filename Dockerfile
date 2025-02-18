FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet build -c Release -o /src/build

WORKDIR /app
FROM build AS publish
RUN dotnet publish "/src/Plugfy.Core.Extension.Library.Runner.DotNet.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
RUN apt-get update && apt-get install -y tzdata

WORKDIR /app
COPY --from=publish /app/publish .

ENV PATH=$PATH:/app

ENTRYPOINT ["./runner"]