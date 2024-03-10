FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine as builder
ADD PocketDockUI /src
WORKDIR /src/PocketDockUI
RUN dotnet publish -r linux-musl-x64 -c Release -p:PublishSingleFile=true -p:PublishTrimmed=True -p:TrimMode=Link --self-contained true -o /src/out
WORKDIR /

FROM mcr.microsoft.com/dotnet/runtime-deps:6.0-alpine3.18
COPY --from=builder /src/out /web
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true 
WORKDIR /web
CMD ./PocketDockUI