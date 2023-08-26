FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine as builder
ADD PocketDockUI /src
WORKDIR /src/PocketDockUI
RUN dotnet publish -r linux-musl-x64 -c Release -p:PublishSingleFile=true -p:PublishTrimmed=True -p:TrimMode=Link --self-contained true -o /src/out
WORKDIR /
ADD https://download.docker.com/linux/static/stable/x86_64/docker-24.0.2.tgz /docker.tar.gz
RUN tar zxvf docker.tar.gz

FROM mcr.microsoft.com/dotnet/runtime-deps:6.0-alpine3.18
COPY --chmod=755 docker-proxy-wrapper /docker-proxy-wrapper
COPY --from=builder --chmod=755 /docker/docker-proxy /usr/local/bin/docker-proxy
COPY --from=builder /src/out /web
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true 
WORKDIR /web
CMD ./PocketDockUI