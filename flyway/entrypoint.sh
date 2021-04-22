#!/bin/bash
flyway -url=jdbc:postgresql://${POSTGRES_HOST}:${POSTGRES_PORT}/${POSTGRES_DB} -user=${POSTGRES_USER} -password=${POSTGRES_PASSWORD} -connectRetries=50 migrate
sleep infinity