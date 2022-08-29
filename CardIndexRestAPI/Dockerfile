#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["CardIndexRestAPI/CardIndexRestAPI.csproj", "CardIndexRestAPI/"]
RUN dotnet restore "CardIndexRestAPI/CardIndexRestAPI.csproj"
COPY . .
WORKDIR "/src/CardIndexRestAPI"
RUN dotnet build "CardIndexRestAPI.csproj" -c Release -o /app/build

FROM build AS publish
ARG VERSION="0.0.0"
RUN dotnet publish "CardIndexRestAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false /p:Version="$VERSION"

FROM base AS final
WORKDIR /app

ENV SOLR_URL=http://solr.default
ENV CARDS_COLLECTION_NAME=kashtankacards
ENV IMAGES_COLLECTION_NAME=kashtankaimages
ENV MAX_RETURN_COUNT=100
ENV LONG_TERM_SEARCH_RADIUS_KM=20.0
ENV SHORT_TERM_SEARCH_RADIUS_KM=200.0
ENV SHORT_TERM_LENGTH_DAYS=30
ENV REVERSE_TIME_GAP_LENGTH_DAYS=14
ENV SIMILARITY_KNN_TOP_K=200

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CardIndexRestAPI.dll"]