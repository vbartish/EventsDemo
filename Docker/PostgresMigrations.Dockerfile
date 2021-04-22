FROM flyway/flyway:latest
COPY flyway/sql /flyway/sql
COPY flyway/entrypoint.sh /entrypoint.sh

USER root
RUN chmod +x /entrypoint.sh

USER flyway
ENTRYPOINT /bin/bash /entrypoint.sh