FROM golang:1.26.6-alpine3.24@sha256:3889b425f035be855a72fb4755265311293b6d414521f0a519d819df32222d83 AS build
WORKDIR /src
RUN apk add --no-cache ca-certificates git \
    && go mod init firemka.local/caddy-build \
    && go get github.com/caddyserver/caddy/v2/cmd/caddy@v2.11.4 \
    && go get \
        github.com/go-jose/go-jose/v3@v3.0.5 \
        github.com/go-jose/go-jose/v4@v4.1.4 \
        github.com/slackhq/nebula@v1.10.3 \
        go.opentelemetry.io/otel@v1.44.0 \
        go.opentelemetry.io/otel/sdk@v1.44.0 \
        golang.org/x/crypto@v0.56.0 \
        google.golang.org/grpc@v1.83.2 \
    && CGO_ENABLED=0 go build -trimpath \
        -ldflags="-s -w -X github.com/caddyserver/caddy/v2.CustomVersion=v2.11.4" \
        -o /out/caddy github.com/caddyserver/caddy/v2/cmd/caddy

FROM alpine:3.24@sha256:28bd5fe8b56d1bd048e5babf5b10710ebe0bae67db86916198a6eec434943f8b
ENV XDG_CONFIG_HOME=/config \
    XDG_DATA_HOME=/data \
    HOME=/data
RUN apk add --no-cache \
        ca-certificates=20260611-r0 \
        libcap=2.78-r0 \
        mailcap=2.1.54-r0 \
        openssl=3.5.8-r0 \
    && addgroup --system --gid 10002 caddy \
    && adduser --system --disabled-password --no-create-home --uid 10002 --ingroup caddy caddy \
    && install -d -o caddy -g caddy /config /data /etc/caddy
COPY --from=build /out/caddy /usr/bin/caddy
COPY --chown=caddy:caddy Caddyfile /etc/caddy/Caddyfile
RUN setcap cap_net_bind_service=+ep /usr/bin/caddy
USER caddy
EXPOSE 80 443 443/udp
VOLUME ["/data", "/config"]
ENTRYPOINT ["caddy"]
CMD ["run", "--config", "/etc/caddy/Caddyfile", "--adapter", "caddyfile"]
