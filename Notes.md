# Submission notes

## Setup

You need the .NET 10 SDK and Docker Desktop.

1. Start SQL Server and RabbitMQ:

   ```
   docker compose up -d
   ```

   SQL Server runs on `localhost,1433` (user `sa`, password `OtAssessment_Dev1`). RabbitMQ runs on `localhost:5672` with `guest`/`guest`, and its management UI is at http://localhost:15672. Both containers have health checks, so `docker compose ps` shows when they're ready.

2. Create the database by running `DatabaseGenerate.sql` against the server, either from SSMS or Azure Data Studio, or with:

   ```
   docker cp DatabaseGenerate.sql ot-sqlserver:/tmp/DatabaseGenerate.sql
   docker exec ot-sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S 127.0.0.1 -U sa -P OtAssessment_Dev1 -i /tmp/DatabaseGenerate.sql
   ```

   The script drops and recreates `OT_Assessment_DB` each time it runs.

3. Start the API and the consumer in two terminals:

   ```
   dotnet run --project src/OT.Assessment.Api --launch-profile http
   dotnet run --project src/OT.Assessment.Consumer
   ```

   The API listens on http://localhost:5000.

4. Run the tester. `test/OT.Assessment.Tester/appsettings.json` already points at `http://localhost:5000/api/player/casinowager`.

   ```
   dotnet run --project test/OT.Assessment.Tester
   ```

### Connection string

The brief's connection string uses Windows authentication (`Integrated Security=SSPI`). That can't work against SQL Server in a Linux container, so `appsettings.json` in both the API and the consumer uses the `sa` login instead. To use a local SQL Server with Windows authentication, change `ConnectionStrings:OT_Assessment_DB` in both files to:

```
Server=localhost;Database=OT_Assessment_DB;Integrated Security=SSPI;TrustServerCertificate=True;
```

## Design

### Projects

| Project | Purpose |
|---|---|
| `OT.Assessment.Api` | The API. Validates wagers and publishes them to RabbitMQ, and serves the two GET endpoints. |
| `OT.Assessment.Consumer` | A worker service that reads wagers from the queue and saves them in batches. |
| `OT.Assessment.Domain` | Models and interfaces. It has no dependencies. |
| `OT.Assessment.Infrastructure` | RabbitMQ and SQL Server code that implements the Domain interfaces. |
| `OT.Assessment.UnitTests` | Tests for paging and the consumer's batch buffer. |

The brief calls the API project `OT.Assessment.App`. I renamed it `OT.Assessment.Api` to follow the usual Api / Domain / Infrastructure naming, which the FAQ allows.

The Api and Consumer depend on the interfaces in Domain and only touch Infrastructure to register services. I kept the dependency rule from Clean Architecture but left out a separate Application layer and MediatR. The problem is a pipeline with two reads and has almost no business logic, so those layers would mostly pass data through.

### Flow

1. `POST api/player/casinowager` validates the wager and publishes it to the durable `casino-wagers` queue as a persistent message. Publisher confirms are on, so the API only answers once RabbitMQ has the message. It returns `202 Accepted` because the wager is queued, not saved yet.
2. The consumer receives messages with a prefetch of 1000 and holds them in a buffer.
3. A loop takes up to 500 wagers at a time and saves them with one stored procedure call using a table-valued parameter.
4. Only after the database commit does the consumer acknowledge the batch, using the last delivery tag with `multiple: true`.

If the consumer crashes before acknowledging, RabbitMQ redelivers the messages. The stored procedure skips wagers it already has, so a redelivered wager is never stored or counted twice. A batch that fails three times is moved to `casino-wagers.dead-letter` so it isn't lost. A message that can't be read as a wager goes straight there.

### Database

- `Provider` and `Game` hold each name once, and wagers point at a game by id.
- `Player` keeps a running `TotalAmountSpend`, so the top spenders query reads an index instead of summing every wager. The insert procedure recalculates the total for each player in the batch from their stored wagers, which keeps it correct even if a message is redelivered.
- `CasinoWager` has `WagerId` as a nonclustered primary key. The clustered index is `(AccountId, CreatedDateTimeUtc DESC, WagerId)`, so a player's latest wagers are one index seek. Clustering on the random `WagerId` would scatter inserts and suit no query.
- Only the fields the endpoints need are stored. `SessionData`, `Theme`, `BrandId`, `CountryCode` and the other ids aren't used by any requirement, so I left them out. Adding a column later is cheap.
- Amounts are `DECIMAL(19,4)`. Dates are converted to UTC and stored as `DATETIME2`.
- `InsertedDateTimeUtc` records when each wager was stored, which is how the load test time below was measured.

I used Dapper with stored procedures. The brief asks for stored procedures, and the batch insert is several steps in one transaction, which is one round trip as a procedure.

## Load test results

Two runs on 2026-11-04 on a Windows 11 laptop, with SQL Server and RabbitMQ in Docker Desktop. The database was recreated before each run.

| Measure | Run 1 | Run 2 |
|---|---|---|
| Requests sent by the tester | 7000 | 7000 |
| Successful (`202 Accepted`) | 7000 | 7000 |
| Failed requests | 0 | 0 |
| Tester duration | 28 s at 250 requests/s | 28 s at 250 requests/s |
| API latency p50 / p95 / p99 | 7.5 / 618 / 1010 ms | 6.2 / 61 / 155 ms |
| API latency max | 7571 ms | 222 ms |
| Wagers stored | 7000 (all distinct) | 7000 (all distinct) |
| Time from first to last insert | 29.7 s | 26.2 s |
| Consumer batches | 517, none failed | 382, none failed |
| Messages left in the main queue | 0 | 0 |
| Messages in the dead letter queue | 0 | 0 |
| Players whose total doesn't match their wagers | 0 | 0 |

Every submitted wager was stored in both runs and nothing was lost. The consumer kept pace with the tester, saving the last wager within about 2 seconds of the tester finishing.

Run 1 was the first time the API had handled load since starting, so its latency includes warm-up (JIT compilation, the first RabbitMQ connection, the SQL connection pool filling). Run 2 is the steadier number.

### GET endpoints during the load

During run 2 a script called both GET endpoints about once a second for the whole test: `topSpenders?count=10`, then the first page of wagers for the current top spender.

| Measure | Result |
|---|---|
| Polls | 45 |
| Non-200 responses | 0 on either endpoint |
| Slowest `topSpenders` response | 11 ms |
| Slowest player page response | 26 ms |

Both endpoints returned data that grew as wagers were saved, and correct results once the queue was empty.

The reconciliation query I used:

```sql
SELECT COUNT(*) AS StoredWagers, COUNT(DISTINCT WagerId) AS DistinctWagers,
       DATEDIFF(MILLISECOND, MIN(InsertedDateTimeUtc), MAX(InsertedDateTimeUtc)) AS StoreDurationMs
FROM dbo.CasinoWager;

SELECT COUNT(*) AS PlayersWithWrongTotal
FROM dbo.Player p
WHERE p.TotalAmountSpend <> (SELECT SUM(Amount) FROM dbo.CasinoWager w WHERE w.AccountId = p.AccountId);
```

## Challenges

- The SQL Server container's health check kept timing out. `sqlcmd` inside the container couldn't resolve `localhost`, and connecting to `127.0.0.1` fixed it.
- Another project's SQL Server container was already using port 1433, so I stopped it for this work.
- The API publishes through one RabbitMQ channel guarded by a semaphore, because channels aren't safe to publish on from several threads. Each publish waits for its confirm, so publishes queue up behind each other. Combined with warm-up, that's where run 1's p95 and max latency come from.

## Improvements for production

- **Faster publishing.** Use a small pool of channels, or publish without waiting on each confirm and track confirms in the background. That would remove the API latency spikes above.
- **Several consumers.** The insert procedure assumes one batch at a time. Running more consumer instances would need `UPDLOCK, HOLDLOCK` hints on the existence checks, or `MERGE`, to stop two batches racing to insert the same provider, game or player.
- **Secrets.** Move connection strings and passwords out of `appsettings.json` into user secrets or a vault, and give the app a SQL login that can only execute the stored procedures.
- **Resilience.** Reconnect to RabbitMQ automatically after a broker restart, and put a circuit breaker around SQL Server.
- **Observability.** Health check endpoints, metrics for queue depth and batch time, and alerts on the dead letter queue.
- **Tests.** Integration tests with Testcontainers that run the whole pipeline against real SQL Server and RabbitMQ.
- **Validation.** Stricter checks on the incoming payload, for example a fixed list of country codes and a maximum amount.
