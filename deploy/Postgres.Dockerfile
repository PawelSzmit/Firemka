FROM postgres:18-alpine@sha256:d3e1620b530c944afa6e887d22eb899824da68e19c52024bf98f5220c88a65b2 AS patched

USER root
RUN apk add --no-cache --upgrade \
        curl=8.22.0-r0 \
        openssl=3.5.8-r0 \
        util-linux=2.42.3-r1 \
    && rm -f /usr/local/bin/gosu

FROM scratch
COPY --from=patched / /
ENV LANG=en_US.utf8 \
    PG_MAJOR=18 \
    PG_VERSION=18.6 \
    PGDATA=/var/lib/postgresql/18/docker
USER postgres
ENTRYPOINT ["docker-entrypoint.sh"]
STOPSIGNAL SIGINT
EXPOSE 5432
VOLUME ["/var/lib/postgresql"]
CMD ["postgres"]
