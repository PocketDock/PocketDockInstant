FROM traefik:2.10
ENV START_PORT=2000
ENV END_PORT=3000

RUN apk add --no-cache curl
ADD traefik.yml /app/traefik.yml
RUN for i in $(seq $START_PORT $END_PORT); do echo $i; done | xargs -I {} echo -e '\n  entrypoint-{}:\n    address: "fly-global-services:{}/udp"' >> /app/traefik.yml
ADD --chmod=755 start.sh /app/start.sh
RUN touch /app/access.log && chmod 777 /app/access.log
ENTRYPOINT /app/start.sh