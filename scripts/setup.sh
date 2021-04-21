#!/bin/bash
export SQLCMDUSER=$SQL_SERVER_USER
export SQLCMDPASSWORD=$SQL_SERVER_PASSWORD
export SQLCMDSERVER=$SQL_SERVER_HOST
export SQLCMDDBNAME=$SQL_SERVER_DATABASE
export PATH="$PATH:/opt/mssql-tools/bin"

for i in {1..50};
do
  sqlcmd -d master  -I -Q "IF NOT EXISTS(SELECT * FROM sys.databases WHERE name = '${SQL_SERVER_DATABASE}') BEGIN CREATE DATABASE [${SQL_SERVER_DATABASE}] END"
    if [ $? -eq 0 ]
    then
        echo "Create database completed."
        break
    else
        echo "Not ready yet, retrying."
        sleep 1
    fi
done

sqlcmd -i /scripts/setup.sql -I

echo "Migration completed."
sleep infinity