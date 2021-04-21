#!/bin/bash
HTTP_CODE=$(curl -H "Accept:application/json" $CONNECT_HOST:$CONNECT_PORT -s -f -w %{HTTP_CODE} -o /dev/null)
until [ $HTTP_CODE -eq 200 ]
do
    echo "Response of curl: $HTTP_CODE"
    echo "Retrying ..."
    sleep 10
    HTTP_CODE=$(curl -H "Accept:application/json" $CONNECT_HOST:$CONNECT_PORT -s -f -w %{HTTP_CODE} -o /dev/null)
done

echo "Response of curl: $HTTP_CODE. Kafka Connect is up and running, moving on."

curl -i -X POST -H "Accept:application/json" -H "Content-Type:application/json" $CONNECT_HOST:$CONNECT_PORT/connectors/ \
-d '{
        "name": "myConnector",
        "config": {
            "connector.class": "io.debezium.connector.sqlserver.SqlServerConnector",
            "database.hostname": '"\"$SQL_SERVER_HOST\""',
            "database.port": '"\"$SQL_SERVER_PORT\""',
            "database.password": '"\"$SQL_SERVER_PASSWORD\""',
            "database.user": '"\"$SQL_SERVER_USER\""',
            "database.dbname": '"\"$SQL_SERVER_DATABASE\""',
            "database.server.name": '"\"$SQL_SERVER_SERVER_NAME\""',
            "table.include.list": '"\"$CDC_TABLES\""',
            "database.history.kafka.bootstrap.servers": '"\"$KAFKA_BOOTSTRAP_SERVERS\""',
            "database.history.kafka.topic": '"\"$KAFKA_HISTORY_TOPIC\""',
            "tombstones.on.delete": "true",
            "database.applicationIntent": "ReadOnly",
            "snapshot.isolation.mode": "snapshot",
            "snapshot.fetch.size": "20000",
            "snapshot.mode": "initial",
            "poll.interval.ms": "100",
            "max.batch.size": "66536",
            "max.queue.size": "131072",
            "tasks.max": "1",
            "topic.creation.default.replication.factor": 2,
            "topic.creation.default.partitions": 5
            }
    }'
echo "\nConnector created"
sleep infinity