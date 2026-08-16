FROM postgres:16-alpine
COPY docker-entrypoint-initdb.d/ /docker-entrypoint-initdb.d/
RUN chmod -R a+rX /docker-entrypoint-initdb.d
