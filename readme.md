# .NET with ksqlDB  

## Intro
Contemplating the capabilities of Apache Kafka, I found myself pondering: Is it possible to query data directly within Kafka? 
It took me some time to muster the resolve to seek an answer to this question, but I'm delighted to say that I have indeed found the solution. 
Yes, it is achievable through KSQL.

## Setup
Tools:
- Zookeeper
- Kafka broker
- Schema-registry
- Ksqldb-server
- Ksqldb-cli
- Visual Studio 2022 

Optional tools:
- kafdrop
- MySQL

Get standalone ksqlDB 

Create a file docker-compose.yml and put in the code below

```yml

version: '2'

services:

  zookeeper:
    image: confluentinc/cp-zookeeper:7.4.0
    hostname: zookeeper
    container_name: zookeeper
    ports:
      - "2181:2181"
    environment:
      ZOOKEEPER_CLIENT_PORT: 2181
      ZOOKEEPER_TICK_TIME: 2000

  broker:
    image: confluentinc/cp-kafka:7.4.0
    hostname: broker
    container_name: broker
    depends_on:
      - zookeeper
    ports:
      - "29092:29092"
    environment:
      KAFKA_BROKER_ID: 1
      KAFKA_ZOOKEEPER_CONNECT: 'zookeeper:2181'
      KAFKA_LISTENER_SECURITY_PROTOCOL_MAP: PLAINTEXT:PLAINTEXT,PLAINTEXT_HOST:PLAINTEXT
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://broker:9092,PLAINTEXT_HOST://localhost:29092
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1
      KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS: 0
      KAFKA_TRANSACTION_STATE_LOG_MIN_ISR: 1
      KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR: 1

  schema-registry:
    image: confluentinc/cp-schema-registry:7.4.0
    hostname: schema-registry
    container_name: schema-registry
    depends_on:
      - zookeeper
      - broker
    ports:
      - "8082:8082"
    environment:
      SCHEMA_REGISTRY_HOST_NAME: schema-registry
      SCHEMA_REGISTRY_KAFKASTORE_BOOTSTRAP_SERVERS: "PLAINTEXT://broker:9092"

  ksqldb-server:
    image: confluentinc/ksqldb-server:0.29.0
    hostname: ksqldb-server
    container_name: ksqldb-server
    depends_on:
      - broker
      - schema-registry
    ports:
      - "8088:8088"
    volumes:
      - "./confluent-hub-components/:/usr/share/kafka/plugins/"
    environment:
      KSQL_LISTENERS: "http://0.0.0.0:8088"
      KSQL_BOOTSTRAP_SERVERS: "broker:9092"
      KSQL_KSQL_SCHEMA_REGISTRY_URL: "http://schema-registry:8082"
      KSQL_KSQL_LOGGING_PROCESSING_STREAM_AUTO_CREATE: "true"
      KSQL_KSQL_LOGGING_PROCESSING_TOPIC_AUTO_CREATE: "true"
      # Configuration to embed Kafka Connect support.
      KSQL_CONNECT_GROUP_ID: "ksql-connect-cluster"
      KSQL_CONNECT_BOOTSTRAP_SERVERS: "broker:9092"
      KSQL_CONNECT_KEY_CONVERTER: "org.apache.kafka.connect.storage.StringConverter"
      KSQL_CONNECT_VALUE_CONVERTER: "io.confluent.connect.avro.AvroConverter"
      KSQL_CONNECT_VALUE_CONVERTER_SCHEMA_REGISTRY_URL: "http://schema-registry:8082"
      KSQL_CONNECT_CONFIG_STORAGE_TOPIC: "_ksql-connect-configs"
      KSQL_CONNECT_OFFSET_STORAGE_TOPIC: "_ksql-connect-offsets"
      KSQL_CONNECT_STATUS_STORAGE_TOPIC: "_ksql-connect-statuses"
      KSQL_CONNECT_CONFIG_STORAGE_REPLICATION_FACTOR: 1
      KSQL_CONNECT_OFFSET_STORAGE_REPLICATION_FACTOR: 1
      KSQL_CONNECT_STATUS_STORAGE_REPLICATION_FACTOR: 1
      KSQL_CONNECT_PLUGIN_PATH: "/usr/share/kafka/plugins"

  ksqldb-cli:
    image: confluentinc/ksqldb-cli:0.29.0
    container_name: ksqldb-cli
    depends_on:
      - broker
      - ksqldb-server
    entrypoint: /bin/sh
    tty: true

  kafdrop:
    image: obsidiandynamics/kafdrop
    restart: "no"
    ports:
      - "9000:9000"
    environment:
      KAFKA_BROKERCONNECT: "broker:9092"
    depends_on:
      - broker

  mysql:
    image: mysql:8.0.19
    hostname: mysql
    container_name: mysql
    ports:
      - "3306:3306"
    environment:
      MYSQL_ROOT_PASSWORD: mysql-pw
      MYSQL_DATABASE: call-center
      MYSQL_USER: example-user
      MYSQL_PASSWORD: example-pw
    volumes:
      - "./mysql/custom-config.cnf:/etc/mysql/conf.d/custom-config.cnf"
 
```

In a terminal navigate to the docker-compose folder and execute

```cmd

    docker-compose up

```


## Basic ksql
Now that we have the necessary infrastructure we can start.

1. Start ksqlDB's interactive CLI
ksqlDB runs as a server which clients connect to in order to issue queries.
Run this command to connect to the ksqlDB server and enter an interactive command-line interface (CLI) session.

```cmd

    docker exec -it ksqldb-cli ksql http://ksqldb-server:8088

```

2. Create a stream
The first thing we're going to do is create a stream. A stream essentially associates a schema with an underlying Kafka topic. 
Here's what each parameter in the *CREATE STREAM* statement does:
*kafka_topic* - Name of the Kafka topic underlying the stream. 
In this case it will be automatically created because it doesn't exist yet, but streams may also be created over topics that already exist.
*value_format* - Encoding of the messages stored in the Kafka topic. 
For JSON encoding, each row will be stored as a JSON object whose keys/values are column names/values. 
For example: {"Id": 1, "User":"Amen", "Message": "Hello World"}

[Check the documentation for more information about streams.](https://docs.ksqldb.io/en/latest/concepts/collections/streams/?_ga=2.207984017.2016512056.1703559960-674955265.1703559960)


```cmd

    CREATE STREAM TweetStream (Id INT, User VARCHAR, Message VARCHAR) 
    WITH (kafka_topic='Tweet', value_format='json', partitions=1);

```


3. Create materialized views
We might also want to keep track of the latest tweets using a materialized view. 
For this we create a table TweetTable by issuing a SELECT statement over the previously created stream. 
Note that the table will be incrementally updated as new tweet data arrives. 
We use the LATEST_BY_OFFSET aggregate function to denote the fact that we are only interested in the latest tweets.

```cmd

CREATE TABLE TweetTable AS
SELECT  LATEST_BY_OFFSET(Id), User
FROM TweetStream
GROUP BY User
EMIT CHANGES;

```
To make it more fun, let us also materialize a derived table (Table TweetView) that captures how many tweets a user has posted.

```cmd

CREATE TABLE TweetView AS
  SELECT COUNT(*) AS MessageCount,
         User,
         COLLECT_LIST(Message) AS Messages       
  FROM TweetStream
  GROUP BY User;

````



4. Run a push query over the stream
Now, let us run a push query over the stream. Run the given query using your interactive CLI session.
This query will output all rows from the Tweet stream which contain "Hi".
This query will never return until it's terminated. 
It will perpetually push output rows to the client as events are written to the Tweet stream.

```cmd

SELECT * FROM TweetStream
  WHERE Message LIKE '%hi%' OR Message LIKE '%Hi%'
  EMIT CHANGES;

```

Leave this query running in the CLI session for now. 
Next, we're going to write some data into the Tweet stream so that the query begins producing output.


5. Populate the stream with events
Run each of the given INSERT statements within the new CLI session, and keep an eye on the CLI session from (4) as you do.
The push query will output matching rows in real time as soon as they're written to the Tweet stream.

```cmd

    INSERT INTO TweetStream (id, User, Message) VALUES (1, 'Amen', 'Hi ksqlDB');
    INSERT INTO TweetStream (id, User, Message) VALUES (2, 'ksqlDb', 'Hello Amen');
    INSERT INTO TweetStream (id, User, Message) VALUES (3, 'Amen', 'Hi Everyone');
    INSERT INTO TweetStream (id, User, Message) VALUES (4, 'Everyone', 'Hi, It looks like it is working');
    INSERT INTO TweetStream (id, User, Message) VALUES (5, 'Amen', 'Yeah, we made it to the highest pick.');

```

6. Run a Pull query against the materialized view
Finally, we run a pull query against the materialized view to retrieve all the Users that are more than 2 tweet count.
In contrast to the previous push query which runs continuously, 
the pull query follows a traditional request-response model retrieving the latest result from the materialized view.

```cmd

    SELECT * from TweetView WHERE MessageCount >= 2;

```


## .NET & ksqlDB

1. Create a basic .NET console app
2. Install Ksql Client

```xml
  <ItemGroup>
    <PackageReference Include="ksqlDb.RestApi.Client" Version="3.4.0" />
  </ItemGroup>
```

3. Create this model

```c#
 public class Tweet : Record
 {
     public int Id { get; set; }
     public string User { get; set; }
     public string Message { get; set; }
 }
 ```

 4. Add this the the program file
 ```c#
// See https://aka.ms/new-console-template for more information
using ksqlDB.RestApi.Client.KSql.Query.Context;
using ksqlDB.RestApi.Client.KSql.Query.Options;
using ksqlDB.RestApi.Client.KSql.Linq;
using Basics.Models;
using System.Runtime.CompilerServices;

// Connect to ksqlDB
var ksqlDbUrl = @"http://localhost:8088";

var contextOptions = new KSqlDBContextOptions(ksqlDbUrl)
{
    ShouldPluralizeFromItemName = false
};

await using var context = new KSqlDBContext(contextOptions);

// Subscribe and consume
using var subscription = context.CreateQueryStream<TweetStream>()
    .WithOffsetResetPolicy(AutoOffsetReset.Latest)
    .Where(p => p.User.Equals("Amen"))
      .Select(l => new { l.Id, l.User, l.Message })
      .Take(2)
      .Subscribe(
          tweetMessage =>
          {
              Console.WriteLine($"{nameof(TweetStream)}: {tweetMessage.Id} - {tweetMessage.User} - {tweetMessage.Message}");
          }, 
          error => 
          { 
              Console.WriteLine($"Exception: {error.Message}"); 
          }, 
          () =>
          {
              Console.WriteLine("Completed");
          }
      );

Console.WriteLine("Press any key to stop the subscription");

Console.ReadKey();
```