# pm_* stages should be kept separate from rest of stages, used as a base image for runc

FROM debian:bookworm-slim as pm_build

# Enable src repos and build screen static binary
RUN sed -i 's/^Types: deb$/Types: deb deb-src/g' /etc/apt/sources.list.d/debian.sources
RUN apt-get update

RUN mkdir /screen
WORKDIR /screen
RUN apt-get build-dep -y --no-install-recommends screen
ENV CFLAGS=-static LDFLAGS=-static CPPFLAGS=-static CFLAGS_APPEND=-static LDFLAGS_APPEND=-static CPPFLAGS_APPEND=-static
RUN apt-get source --build screen
RUN cp screen-*/debian/screen/usr/bin/screen /usr/local/bin/screen

# Setup packages to extract to distroless image
RUN mkdir /deb /extract
WORKDIR /deb
RUN apt-get download ncurses-bin libtinfo6
RUN ls *.deb | xargs -I {} dpkg --extract {} /extract

RUN apt update \
    && apt install -y --no-install-recommends \
    ca-certificates \
    curl \
    wget

RUN wget https://github.com/yudai/gotty/releases/download/v1.0.1/gotty_linux_amd64.tar.gz && \
    tar zxvf gotty_linux_amd64.tar.gz && \
    mv gotty /usr/local/bin/gotty && \
    rm gotty_linux_amd64.tar.gz 

RUN echo 'nameserver 1.1.1.1' > /resolv_conf
RUN echo 'app:x:2000:2000:Linux User,,,:/home/app:/sbin/nologin' >> /etc_passwd
#Disable shortcuts on screen
RUN echo -e 'multiuser on\naclchg app -x+r+w "#?"' > /etc_screenrc

RUN mkdir /app
WORKDIR /app
RUN curl -Lo install.sh https://get.pmmp.io && chmod +x install.sh && ./install.sh -r


FROM gcr.io/distroless/cc-debian12:debug as pm_base

RUN ["/busybox/ln", "-s", "/busybox/busybox", "/bin/sh"]
RUN ln -s /busybox/busybox /bin/sleep

COPY --from=pm_build /usr/local/bin/screen /usr/local/bin/screen
COPY --from=pm_build /extract /
COPY --from=pm_build /lib/terminfo/ /lib/terminfo/
COPY --from=pm_build /usr/local/bin/gotty /usr/local/bin/gotty
COPY --from=pm_build /app /app
COPY --from=pm_build /resolv_conf /etc/resolv.conf
COPY --from=pm_build /etc_passwd /etc/passwd
COPY --from=pm_build /etc_screenrc /etc/screenrc

COPY --chmod=755 pm/console.sh /app/console.sh
COPY --chmod=755 pm/start.sh /app/start.sh
COPY --chmod=755 pm/pmloop.sh /app/pmloop.sh

####################

FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine as dotnet
RUN mkdir /out
COPY McQuery/ /McQuery
RUN dotnet publish -r linux-musl-x64 -c Release -p:PublishSingleFile=true -p:PublishTrimmed=True -p:TrimMode=Link --self-contained true -o /out /McQuery/

FROM alpine:3.18 as download
ADD https://github.com/angelbarrera92/basic-auth-reverse-proxy/releases/download/v0.1.5/basic-auth-reverse-proxy_0.1.5_linux_amd64.tar.gz /basic-auth-reverse-proxy.tar.gz
ADD https://github.com/filebrowser/filebrowser/releases/download/v2.23.0/linux-amd64-filebrowser.tar.gz /
ADD https://download.docker.com/linux/static/stable/x86_64/docker-24.0.2.tgz /docker.tar.gz
RUN tar zxvf basic-auth-reverse-proxy.tar.gz
RUN tar zxvf linux-amd64-filebrowser.tar.gz
RUN tar zxvf docker.tar.gz
RUN apk add patch curl
RUN curl -Lo /pm_install.sh https://get.pmmp.io
ADD host/pm_install.patch /
# Disable post-install check to make sure the php binary can run
RUN patch /pm_install.sh < /pm_install.patch

FROM alpine:3.18
#NOTE: If running into iptables issues, make sure this is using the legacy iptables
RUN apk add --no-cache \
        curl \
        ca-certificates \
        iptables \
        kmod \
        procps \
        runc \
        bash \
        busybox-static \
        # .NET dependencies
        #From: https://github.com/dotnet/dotnet-docker/blob/9daba1c6f11971a721cc04f9b7c617e129410f17/src/runtime-deps/6.0/alpine3.17/amd64/Dockerfile
        krb5-libs \
        libgcc \
        libintl \
        libssl3 \
        libstdc++ \
        zlib
        # End .NET dependencies

RUN mkdir -p /filebrowser/rootfs/app
RUN mkdir -p /usr/local/bin/ && curl -Lo /usr/local/bin/netns "https://github.com/genuinetools/netns/releases/download/v0.5.3/netns-linux-amd64" && chmod 755 /usr/local/bin/netns
COPY --from=download --chmod=755 /basic-auth-reverse-proxy /usr/local/bin/basic-auth-reverse-proxy
COPY --from=download --chmod=755 filebrowser /filebrowser/rootfs/app/filebrowser
COPY --from=download --chmod=755 /docker/docker-proxy /usr/local/bin/docker-proxy
COPY --from=download --chmod=755 /pm_install.sh /pm_install.sh
COPY --from=dotnet --chmod=755 /out/McQuery* /usr/local/bin/

RUN mkdir -p /filebrowser/rootfs/bin/ && \
        cp /bin/busybox.static /filebrowser/rootfs/bin/busybox && \
        ln -s /bin/busybox /filebrowser/rootfs/bin/sh && \
        ln -s /bin/busybox /filebrowser/rootfs/bin/sleep && \
        ln -s /bin/busybox /filebrowser/rootfs/bin/mkdir
COPY host/config.json /filebrowser/
COPY --chmod=755 filebrowser/start.sh /filebrowser/rootfs/app/start.sh
RUN mkdir -p /filebrowser/rootfs/etc && echo 'app:x:2000:2000:Linux User,,,:/home/app:/sbin/nologin' >> /filebrowser/rootfs/etc/passwd
COPY filebrowser/config.json /filebrowser/rootfs/app/
COPY filebrowser/users.json /filebrowser/rootfs/app/

RUN mkdir /data
COPY host/config.json /pm/
COPY host/authn.yaml /authn.yaml
COPY --from=pm_base / /pm/rootfs
COPY --chmod=755 host/start.sh /start.sh
COPY --chmod=755 host/iptables_setup.sh /iptables_setup.sh
CMD /start.sh