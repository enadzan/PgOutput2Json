# PgOutput2Json library for .NET Core
The PgOutput2Json library for .NET Core uses PostgreSQL logical replication to push changes, as JSON messages, from database tables to a message broker, such as RabbitMq (implemented) or Redis (todo). 

Logical replication is a means to stream messages generated by PostgreSQL [logical decoding plugins](https://www.postgresql.org/docs/current/logicaldecoding.html) to a client. `pgoutput` is the standard logical decoding output plug-in in PostgreSQL 10+. It is maintained by the PostgreSQL community, and used by PostgreSQL itself for logical replication. This plug-in is always present so no additional libraries need to be installed. 

The PgOutput2Json library converts `pgoutput` messages to JSON format which can then be used by any .NET Core application. One example of such application is PgOutput2Json.RabbitMq library which forwards these JSON messages to RabbitMQ server. 

⚠️ **PgOutput2Json is in early stages of development. Use at your own risk.**  
⚠️ **[Npgsql](https://github.com/npgsql/npgsql) is used for connecting to PostgreSQL. Replication support is new in Npgsql and is considered a bit experimental.** 

## Setup PostgreSQL

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

### Create replication slot
In the same database, create a replication slot, which will hold the state of the replication stream:
```
SELECT * FROM pg_create_logical_replication_slot('my_slot', 'pgoutput');
```
Make sure you specify `pgoutput` as a second parameter. If your application goes down, the slot persistently records the last data streamed to it, and allows resuming the application at the point where it left off.

⚠️ **Do not forget to drop the slot when you are done testing the Pgoutput2Json library. Otherwise PostgreSQL may not be able to remove/recycle WAL files. Use: `SELECT * FROM pg_drop_replication_slot('my_slot');`**

## Setup RabbitMQ

### Create topic exchange
Create a `durable` `topic` type `exchange` where PgOutput2Json will be pushing JSON files. We will assume the name of the exchange is `my_exchange` in the `/` virtual host.

### Create and bind a queue to hold the JSON messages
Create a `durable` queue named `my_queue` and bind it to the exchange created in the previous step. Use `public.#` as the routing key. That way, it will receive JSON messages for all tables in the `public` schema. **Use different schema name if your tables are in different schema**

## Create .NET Core Worker Service
Finally, create a .NET Core Worker Service and add the following package references:
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
            .WithPgReplicationSlot("my_slot")
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
Run the code, and with a little luck you should see JSON messages being pushed in the `my_queue` in RabbitMQ. The routing key will be in the form: `schema.table.key_partition`. Since we did not configure anything specific in the PgOutput2Json, the `key_partition` will always be `0`.

## Features
TODO:
- Describe JSON and JsonOptions
- Describe batching feature
- Describe partition feature
