#!/bin/bash
# Creates OT_Assessment_DB the first time the stack starts.
# DatabaseGenerate.sql drops the database, so it only runs when the database doesn't exist yet.
set -e

SQLCMD="/opt/mssql-tools18/bin/sqlcmd -C -S sqlserver -U sa -P $MSSQL_SA_PASSWORD -b"

DATABASE_EXISTS=$($SQLCMD -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = 'OT_Assessment_DB'")

if [ "$DATABASE_EXISTS" = "0" ]; then
    echo "Creating OT_Assessment_DB"
    $SQLCMD -i /scripts/DatabaseGenerate.sql
else
    echo "OT_Assessment_DB already exists, leaving it as it is"
fi
