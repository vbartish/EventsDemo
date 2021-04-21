#!/bin/bash
CURRENT_SERVER_NAME=$(sqlcmd -h -1 -Q "set nocount on; select srvname from master.dbo.sysservers" | tr -d '[:space:]')
EXPECTED_SERVER_NAME=$(sqlcmd -h -1 -Q "set nocount on; select SERVERPROPERTY('ServerName')" | tr -d '[:space:]')

echo ${CURRENT_SERVER_NAME}
echo ${EXPECTED_SERVER_NAME}

if [ $CURRENT_SERVER_NAME != $EXPECTED_SERVER_NAME ]
then
  echo "Updating sql server name because it was not set to itself"
  sqlcmd -Q "sp_dropserver '${CURRENT_SERVER_NAME}'"
  sqlcmd -Q "sp_addserver '${EXPECTED_SERVER_NAME}', local"
fi

echo "enabling snapshot isolation"
sqlcmd -h -1 -Q "set nocount on; ALTER DATABASE ${SQLCMDDBNAME} SET ALLOW_SNAPSHOT_ISOLATION ON"
echo "snapshot isolation enabled"

CDC_ENABLED=$(sqlcmd -h -1 -Q "set nocount on; select is_cdc_enabled from sys.databases where name='${SQLCMDDBNAME}'" | tr -d '[:space:]')
if [ $CDC_ENABLED -lt 1 ]
then
    echo "Enabling CDC for ${SQLCMDDBNAME}"
    sqlcmd -Q "EXEC sys.sp_cdc_enable_db"
fi

IFS=', ' read -r -a TABLES <<< $CDC_TABLES

echo "Tracking tables:"
echo "${TABLES[@]}"

for table in "${TABLES[@]}"
do
    SCHEMALESS_TABLE_NAME=${table#"dbo."}
    CDC_TABLE_NAME=${table//"."/"_"}_CT
    TABLE_EXISTS=$(sqlcmd -h -1 -Q "set nocount on; SELECT 'exists' FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'cdc' AND TABLE_NAME = '${CDC_TABLE_NAME}'" | tr -d '[:space:]')
    if [ -z $TABLE_EXISTS ]
    then
        echo "Enabling CDC for ${table}"
        sqlcmd -Q "EXEC sys.sp_cdc_enable_table @source_schema = 'dbo', @source_name = '${SCHEMALESS_TABLE_NAME}', @supports_net_changes = 1, @role_name=NULL"
    fi
done