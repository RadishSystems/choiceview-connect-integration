FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
ENV PATH="${PATH}:/root/.dotnet/tools"

# copy csproj and restore as distinct layers
COPY *.sln .
COPY ChoiceViewAPI/*.csproj ./ChoiceViewAPI/
RUN apt-get update && apt-get install --yes zip
RUN dotnet tool install -g Amazon.Lambda.Tools && dotnet restore ./ChoiceViewAPI/ChoiceViewAPI.csproj

# copy everything else and build app
COPY ChoiceViewAPI/. ./ChoiceViewAPI/
CMD mkdir -p /output && dotnet lambda package -pl /ChoiceViewAPI -o /output/ChoiceViewAPI.zip
