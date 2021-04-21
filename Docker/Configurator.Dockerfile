FROM mcr.microsoft.com/mssql-tools

ENV PATH $PATH:/root/.dotnet/tools

RUN mkdir /scripts

COPY ./scripts /scripts

RUN apt-get --assume-yes install jq

# grant permissions for the script to be executable
RUN chmod +x /scripts/setup.sh
RUN chmod +x /scripts/enable_cdc.sh
RUN chmod +x /scripts/connector.sh

ENTRYPOINT /bin/bash /scripts/setup.sh