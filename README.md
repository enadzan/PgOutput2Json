# PgOutput2Json library for .NET Core
The PgOutput2Json library for .NET Core uses PostgreSQL logical replication to push changes, as JSON messages, from database tables to a message broker, such as RabbitMq (implemented) or Redis (todo). 

Logical replication is a means to stream messages generated by PostgreSQL [logical decoding plugins](https://www.postgresql.org/docs/current/logicaldecoding.html) to a client. `pgoutput` is the standard logical decoding output plug-in in PostgreSQL 10+. It is maintained by the PostgreSQL community, and used by PostgreSQL itself for logical replication. This plug-in is always present so no additional libraries need to be installed. 

The PgOutput2Json library converts `pgoutput` messages to JSON format which can then be used by any .NET Core application. One example of such application is PgOutput2Json.RabbitMq library which forwards these JSON messages to RabbitMQ server. 

⚠️ **PgOutput2Json is in early stages of development. Use at your own risk.**  
⚠️ **[Npgsql](https://github.com/npgsql/npgsql) is used for connecting to PostgreSQL. Replication support is new in Npgsql and is considered a bit experimental.** 

## 1. Quick Start

### Configure postgresql.conf

First set the configuration options in `postgresql.conf`:
```
wal_level = logical
```
The other required settings have default values that are sufficient for a basic setup. **PostgreSQL must be restarted after this change**.

### Create a user that will be used to start the replication
Login to PostgreSQL with privileges to create users, and execute: 
```
CREATE USER pgoutput2json WITH
	PASSWORD '_your_password_here_'
	REPLICATION;
```
If you will be connecting to the PostgreSQL with this user, from a different machine (non-local connection), don't forget to modify `pg_hba.conf`. If `pg_hba.conf` is modified, you have to SIGHUP the server for the changes to take effect, run `pg_ctl reload`, or execute `SELECT pg_reload_conf()`.

### Create publication
Connect to the database that holds the tables you want to track, and create a publication that specifies the tables and actions for publishing:
```
CREATE PUBLICATION my_publication
    FOR TABLE my_table1, my_table2
    WITH (publish = 'insert, update, delete, truncate');
```
In the C# code examples below, we will assume the database name is `my_database`.

### Create .NET Core Worker Service
Finally, create a .NET Core Worker Service and add the following package reference:
```
dotnet add package PgOutput2Json
```
Add `using PgOutput2Json;` to the Worker.cs, and use this code in the Worker class:
```
public class Worker : BackgroundService
{
    private readonly ILoggerFactory _loggerFactory;

    public Worker(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // This code assumes PostgreSQL is on localhost
        using var pgOutput2Json = PgOutput2JsonBuilder.Create()
            .WithLoggerFactory(_loggerFactory)
            .WithPgConnectionString("server=localhost;database=my_database;username=pgoutput2json;password=_your_password_here_")
            .WithPgPublications("my_publication")
	    .WithMessageHandler((json, table, key, partition) =>
            {
                Console.WriteLine($"{table}: {json}");
            })
            .Build();

        await pgOutput2Json.Start(stoppingToken);
    }
}
```
Run the code, and you should see table names and JSON messages being printed in the console every time you insert, update or delete a row from the tables you specified in the previous step.

Note that this "quick start" version is working with a **temporary replication slot**. That means it will not see any database changes that were done while the worker was stopped. See section 3. in this document if you want to work with a permanent replication slot.

## 2. Using Rabbit MQ

This document assumes you have a running RabbitMQ instance working on the default port, with the default `guest`/`guest` user allowed from local host.

⚠️ First, setup the database, as described in the Quick Start section above.

### Create topic exchange
In RabbitMQ, create a `durable` `topic` type `exchange` where PgOutput2Json will be pushing JSON files. We will assume the name of the exchange is `my_exchange` in the `/` virtual host.

### Create and bind a queue to hold the JSON messages
Create a `durable` queue named `my_queue` and bind it to the exchange created in the previous step. Use `public.#` as the routing key. That way, it will receive JSON messages for all tables in the `public` schema. **Use different schema name if your tables are in different schema**

### Create .NET Core Worker Service
Create a .NET Core Worker Service and add the following package reference:
```
dotnet add package PgOutput2Json.RabbitMq
```
Add `using PgOutput2Json.RabbitMq;` to the Worker.cs, and use this code in the Worker class:
```
public class Worker : BackgroundService
{
    private readonly ILoggerFactory _loggerFactory;

    public Worker(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // This code assumes PostgreSQL and RabbitMQ are on the localhost
	
        using var pgOutput2Json = PgOutput2JsonBuilder.Create()
            .WithLoggerFactory(_loggerFactory)
            .WithPgConnectionString("server=localhost;database=my_database;username=pgoutput2json;password=_your_password_here_")
            .WithPgPublications("my_publication")
            .UseRabbitMq(options =>
            {
                options.HostNames = new[] { "localhost" };
                options.Username = "guest";
                options.Password = "guest";
		options.VirtualHost = "/";
		options.ExchangeName = "my_exchange";
            })
            .Build();

        await pgOutput2Json.Start(stoppingToken);
    }
}
```
Run the code, and with a little luck you should see JSON messages being pushed in the `my_queue` in RabbitMQ, when you make change in the tables specified in `my_publication`. The routing key will be in the form: `schema.table.key_partition`. Since we did not configure anything specific in the PgOutput2Json, the `key_partition` will always be `0`.

## 3. Working with permanent replication slots
TODO

## 4. Batching confirmations to RabbitMQ
TODO

## 5. Partitions by key
TODO

## 6. JSON options
TODO
