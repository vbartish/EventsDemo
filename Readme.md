# Welcome to the events aggregation sandbox

This project intended for experiments and demo purposes around events aggregation.

# Dependencies
1. Project Tye https://github.com/dotnet/tye
    - to install it follow steps here https://github.com/dotnet/tye/blob/main/docs/getting_started.md


## Demo steps
1. get Tye, set up SQL Server DB and get fake data gen built using GRPC service and client 
   (for those who are not familiar with GRPC and protobuf - visit https://docs.microsoft.com/en-us/aspnet/core/grpc/?view=aspnetcore-5.0)
2. get CDC enabled on SQL Server (More about SQL Server CDC here https://docs.microsoft.com/en-us/sql/relational-databases/track-changes/about-change-data-capture-sql-server?view=sql-server-ver15)
3.  Get kafka infrastructure and utils:
   - Learning resources: https://kafka.apache.org/, https://www.confluent.io/
   - Confluent`s github https://github.com/confluentinc/
   - Confluent`s docker hub https://hub.docker.com/u/confluentinc
4. get Kafka Connect, and Debezium connector
   - https://docs.confluent.io/platform/current/connect/index.html#
   - https://debezium.io/


Nice! you've got CDC running!

### cheat sheet

- if connector misbehaves try bouncing the task via Kafka Connect REST API.
  For the docs see:
   1. https://docs.confluent.io/3.2.0/connect/managing.html#managing-running-connectors
   2. https://docs.confluent.io/platform/current/connect/references/restapi.html
- if you are using SQL server of a certain version - it could have a problem with KB4338890 (https://support.microsoft.com/en-us/topic/kb4338890-fix-non-yielding-scheduler-error-and-sql-server-appears-unresponsive-in-sql-server-2014-2016-and-2017-531a3518-df07-30a8-c789-c6b294c5f4f1). 
  This will make it unstable. Fix would be to bounce SQL server container, kill the connector and re-create it using these steps: 
   a) find the configurator container in docker 
   b) docker exec -it bin/sh 
   c) run `curl -i -X POST -H "Accept:application/json" -H "Content-Type:application/json" $CONNECT_HOST:$CONNECT_PORT/connectors/ -d '{ "name": "myConnector", "config": { "connector.class": "io.debezium.connector.sqlserver.SqlServerConnector", "database.hostname": '"\"$SQL_SERVER_HOST\""', "database.port": '"\"$SQL_SERVER_PORT\""', "database.password": '"\"$SQL_SERVER_PASSWORD\""', "database.user": '"\"$SQL_SERVER_USER\""', "database.dbname": '"\"$SQL_SERVER_DATABASE\""', "database.server.name": '"\"$SQL_SERVER_SERVER_NAME\""', "table.include.list": '"\"$CDC_TABLES\""', "database.history.kafka.bootstrap.servers": '"\"$KAFKA_BOOTSTRAP_SERVERS\""', "database.history.kafka.topic": '"\"$KAFKA_HISTORY_TOPIC\""', "tombstones.on.delete": "true", "database.applicationIntent": "ReadOnly", "snapshot.isolation.mode": "snapshot", "snapshot.fetch.size": "20000", "snapshot.mode": "initial", "poll.interval.ms": "100", "max.batch.size": "66536", "max.queue.size": "131072", "tasks.max": "1" } }'` (basically re-creates connector in the same way as it is done in scripts/connector.sh)