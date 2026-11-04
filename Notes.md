# Submission notes

## How to run it

You need Git and Docker Desktop. Run every command from the root of the repository:

```
git clone https://github.com/TlotloS/glowing-waffle.git
cd glowing-waffle
```

1. Start everything:
   ```
   docker compose up -d --build
   ```
   This starts SQL Server, RabbitMQ, the API and the consumer. On the first run it also creates the database from `DatabaseGenerate.sql`. Later runs keep the existing data.
2. Open Swagger UI at http://localhost:5000/swagger to try the endpoints.
3. Run the tester:
   ```
   docker compose run --rm tester
   ```
   It runs in its own container and sends wagers to the `api` container, using the settings in `docker/tester-appsettings.json`. The container is removed when the test finishes.

   With the .NET 10 SDK installed, `dotnet run --project test/OT.Assessment.Tester` runs the same test from your machine. That version already points at `http://localhost:5000/api/player/casinowager`.

| Service | Address | Login |
|---|---|---|
| API and Swagger UI | http://localhost:5000/swagger | |
| RabbitMQ management UI | http://localhost:15672 | `guest` / `guest` |
| SQL Server | `localhost,1433` | `sa` / `OtAssessment_Dev1` |

To run the API and consumer from an IDE instead, start only the infrastructure with `docker compose up -d sqlserver rabbitmq db-init`, then run both projects. The API listens on port 5000 either way, so stop the `api` container first if it's running.

Windows authentication doesn't work with SQL Server in a Linux container, so the apps use the `sa` login. For a local SQL Server, change `ConnectionStrings:OT_Assessment_DB` in both `appsettings.json` files to `Server=localhost;Database=OT_Assessment_DB;Integrated Security=SSPI;TrustServerCertificate=True;`.

## Design

| Project | What it does |
|---|---|
| `OT.Assessment.Api` | Validates wagers, puts them on the queue, and serves the two GET endpoints |
| `OT.Assessment.Consumer` | Reads wagers off the queue and saves them in batches |
| `OT.Assessment.Domain` | Models and interfaces, with no dependencies |
| `OT.Assessment.Infrastructure` | RabbitMQ and SQL Server code |
| `OT.Assessment.UnitTests` | Tests for paging and batching |

The brief names the API project `OT.Assessment.App`. I renamed it `Api` to match the usual Api / Domain / Infrastructure naming. I left out an Application layer and MediatR because there's almost no business logic for them to hold.

How a wager moves through the system:

1. The API publishes the wager to a durable queue and waits for RabbitMQ to confirm it. It returns `202 Accepted`, since the wager is queued but not saved yet.
2. The consumer collects up to 500 wagers and saves them with one stored procedure call.
3. It acknowledges the messages only after the database commit.

If the consumer crashes before step 3, RabbitMQ delivers the messages again. The stored procedure skips wagers it already has, so nothing is saved twice.

If a batch fails 3 times, the consumer saves its wagers one at a time. The good ones are stored and only the wager that keeps failing goes to the dead letter queue. I tested this with 3 good wagers and 1 that was too long for the database: the 3 were saved and the bad one ended up in the dead letter queue.

Database choices:

- Provider and game names are stored once in their own tables.
- `Player.TotalAmountSpend` is kept up to date on every batch, so the top spenders query doesn't sum every wager.
- The wager table is clustered on player and date, so a player's latest wagers come back in one index seek.
- I only store the fields the endpoints use. The rest of the payload, like `SessionData` and `CountryCode`, isn't needed.
- I used Dapper with stored procedures because the brief asks for stored procedures, and the batch insert runs as one transaction in one call.

## Load test results

The database was recreated before each run.

| | Run 1 | Run 2 |
|---|---|---|
| Requests sent / failed | 7000 / 0 | 7000 / 0 |
| Wagers stored | 7000 | 7000 |
| Time to store them all | 29.7 s | 26.2 s |
| API p95 latency | 618 ms | 61 ms |
| Left in queue / dead letter queue | 0 / 0 | 0 / 0 |
| Player totals that didn't match | 0 | 0 |

No wagers were lost. The tester runs for 28 seconds, and the consumer finished about 2 seconds after it. Run 1 was slower because the API was still warming up.

During run 2 I called both GET endpoints every second. All 45 calls returned 200, and none took longer than 26 ms.

I checked the counts with:

```sql
SELECT COUNT(*), COUNT(DISTINCT WagerId) FROM dbo.CasinoWager;
```

## Challenges

- The SQL Server health check kept timing out because `localhost` didn't resolve inside the container. Using `127.0.0.1` fixed it.
- The API publishes on one RabbitMQ channel and waits for each confirm, so requests queue up behind each other under load.

## What I'd add for production

- A pool of RabbitMQ channels, to remove the publishing bottleneck
- Locking in the insert procedure, so more than one consumer can run safely
- Secrets in a vault instead of `appsettings.json`
- Automatic reconnects to RabbitMQ
- Health checks, metrics and alerts on the dead letter queue
- Integration tests against real SQL Server and RabbitMQ
