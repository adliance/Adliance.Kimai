FROM mcr.microsoft.com/dotnet/sdk:10.0 AS setversion
ARG VERSION=0.0.0.0
RUN dotnet tool install -g dotnet-setversion
ENV PATH="${PATH}:/root/.dotnet/tools"
COPY ./src/ /src/
WORKDIR /src/Adliance.Kimai.Vacations
RUN setversion $VERSION

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY --from=setversion /src/Adliance.Kimai.Client/ Adliance.Kimai.Client/
COPY --from=setversion /src/Adliance.Kimai.Vacations/ Adliance.Kimai.Vacations/
RUN dotnet publish "Adliance.Kimai.Vacations/Adliance.Kimai.Vacations.csproj" \
    --configuration Release \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
EXPOSE 80
ENV ASPNETCORE_HTTP_PORTS=80
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Adliance.Kimai.Vacations.dll"]
