# Base image
FROM docker.io/library/ubuntu:22.04

# Install dependencies
RUN apt-get update && apt-get install -y \
    sudo git curl wget software-properties-common

# Install .NET 9 SDK
RUN wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
    sudo dpkg -i packages-microsoft-prod.deb && \
    rm packages-microsoft-prod.deb && \
    sudo apt-get update && \
    sudo apt-get install -y dotnet-sdk-9.0

RUN apt-get install -y \
    libfontconfig1 libx11-6 libxext6 libice6 libsm6 libxt6 libxrender1
