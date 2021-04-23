# Welcome to the events aggregation sandbox

This project intended for experiments and demo purposes around events aggregation. 

*DISCLAIMER:* 
current state of the repo is just it - sandbox. So you'll find a lot of place for improvement in code styling, DRY, tests and other things.

# Dependencies
1. Project Tye https://github.com/dotnet/tye (and as a consequence Docker)
    - to install it follow steps here https://github.com/dotnet/tye/blob/main/docs/getting_started.md
2. Rest of the stuff we're getting from dockerhub or building on our own. However you might want to have a MS SQL management studio and PGAdmin (or whatever the tool you prefer) on quick access.

## Demo steps (tagged in the repo using convention `step<number>` for example `step1`)
1. Get tye and set up SQL Server DB and schema
2. Get SQL server DB fake data gen built using GRPC service and client (for those who are not familiar with GRPC and protobuf - visit https://docs.microsoft.com/en-us/aspnet/core/grpc/?view=aspnetcore-5.0)
3. Get CDC enabled on SQL Server (More about SQL Server CDC here https://docs.microsoft.com/en-us/sql/relational-databases/track-changes/about-change-data-capture-sql-server?view=sql-server-ver15)
4. Get kafka infrastructure and utils:
    - Learning resources: https://kafka.apache.org/, https://www.confluent.io/
    - Confluent`s github https://github.com/confluentinc/
    - Confluent`s docker hub https://hub.docker.com/u/confluentinc
5. Get Kafka Connect, and Debezium connector
    - https://docs.confluent.io/platform/current/connect/index.html#
    - https://debezium.io/


Nice! you've got CDC running!

6. Get utils prepared (pipeline, background worker)
7. Prepare Aggregator state store
8. Consume! 
   - cheat sheet: if connector misbehaves try 
      a) find the configurator container in docker 
      b) docker exec -it <containerId> bash/sh 
      c) run `curl -i -X POST -H "Accept:application/json" -H "Content-Type:application/json" $CONNECT_HOST:$CONNECT_PORT/connectors/ -d '{ "name": "myConnector", "config": { "connector.class": "io.debezium.connector.sqlserver.SqlServerConnector", "database.hostname": '"\"$SQL_SERVER_HOST\""', "database.port": '"\"$SQL_SERVER_PORT\""', "database.password": '"\"$SQL_SERVER_PASSWORD\""',  "database.user": '"\"$SQL_SERVER_USER\""', "database.dbname": '"\"$SQL_SERVER_DATABASE\""', "database.server.name": '"\"$SQL_SERVER_SERVER_NAME\""', "table.include.list": '"\"$CDC_TABLES\""',  "database.history.kafka.bootstrap.servers": '"\"$KAFKA_BOOTSTRAP_SERVERS\""', "database.history.kafka.topic": '"\"$KAFKA_HISTORY_TOPIC\""', "tombstones.on.delete": "true", "database.applicationIntent": "ReadOnly", "snapshot.isolation.mode": "snapshot", "snapshot.fetch.size": "20000", "snapshot.mode": "initial", "poll.interval.ms": "100", "max.batch.size": "66536", "max.queue.size": "131072", "tasks.max": "1" } }'` (basically re-creates connector in the same way as it is done in scripts/connector.sh)
      d) more connect API data here https://docs.confluent.io/3.2.0/connect/managing.html#using-the-rest-interface
      e) sometimes it might be caused by sql server jobs failing - this should be visible in logs of SQL SERVER. Just bounce the SQL SERVER container
9. Aggregate! (note the transactional outbox)
10. Publish!

Aaand here you are. 

## Further recommendations
0. Retention policy and Compensation API!!!
1. bulkhead
2. instrumentation 
3. performance measurements and optimizations (aggregation approach here could be optimized)

## Ideas on what to wire up next
1. Zipkin
2. Build something to demonstrate windowing technique
3. Blazor front end to wrap this up with some UI
4. Consider building alternative sln using actor framework