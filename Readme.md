# Welcome to the events aggregation sandbox

This project intended for experiments and demo purposes around events aggregation.

# Dependencies
1. Project Tye https://github.com/dotnet/tye
    - to install it follow steps here https://github.com/dotnet/tye/blob/main/docs/getting_started.md


## Demo steps
1. get tye and set up SQL Server DB and schema
2. get SQL server DB fake data gen built using GRPC service and client (for those who are not familiar with GRPC and protobuf - visit https://docs.microsoft.com/en-us/aspnet/core/grpc/?view=aspnetcore-5.0)
3. get CDC enabled on SQL Server (More about SQL Server CDC here https://docs.microsoft.com/en-us/sql/relational-databases/track-changes/about-change-data-capture-sql-server?view=sql-server-ver15)
4. Get kafka infrastructure and utils:
    - Learning resources: https://kafka.apache.org/, https://www.confluent.io/
    - Confluent`s github https://github.com/confluentinc/
    - Confluent`s docker hub https://hub.docker.com/u/confluentinc
