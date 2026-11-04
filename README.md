# OT Assessment: casino wager capture

An API, a background consumer and a SQL Server database that capture casino wagers and report on them. The API puts each wager on a RabbitMQ queue, the consumer saves them to SQL Server in batches, and two endpoints read the results back.

| Endpoint | What it does |
|---|---|
| `POST api/player/casinowager` | Accepts a wager and queues it |
| `GET api/player/{playerId}/casino?page=1&pageSize=10` | A player's latest wagers, one page at a time |
| `GET api/player/topSpenders?count=10` | Players with the highest total spend |

## Quick start

You need Docker Desktop, and the .NET 10 SDK to run the load tester.

```
docker compose up -d --build
```

Then open Swagger UI at http://localhost:5000/swagger. To run the load test:

```
dotnet run --project test/OT.Assessment.Tester
```

## More detail

[Notes.md](Notes.md) has the setup options, design choices, load test results, challenges and what I'd add for production.

The original assessment brief is in [docs/assessment-brief.md](docs/assessment-brief.md).
