# Hacker News Best Stories API

A RESTful API built with **ASP.NET Core 10** that retrieves the top *n* best stories from
[Hacker News](https://news.ycombinator.com/), ordered by score descending.

---

## Quick Start

### Prerequisites

| Tool | Version |
|------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0+ |
| Git | Any recent |

### Run the application

```bash
git clone https://github.com/maiamateusotavio/HackerNewsApi.git
cd HackerNewsApi

# Restore & run
dotnet run --project HackerNews.Api

# The API starts at http://localhost:5030
# Swagger UI available at http://localhost:5030/swagger
```

### Run the tests

```bash
dotnet test
```

### Try it

```bash
# Get the 10 best stories (default)
curl http://localhost:5030/api/stories/best

# Get the top 5
curl http://localhost:5030/api/stories/best?n=5

# Get the top 50
curl http://localhost:5030/api/stories/best?n=50
```

---

## API Specification

### `GET /api/stories/best?n={count}`

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| `n` | int | 10 | 1–500 | Number of stories to return |

#### Success Response — `200 OK`

```json
[
  {
    "title": "A uBlock Origin update was rejected from the Chrome Web Store",
    "uri": "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
    "postedBy": "ismaildonmez",
    "time": "2019-10-12T13:43:01+00:00",
    "score": 1716,
    "commentCount": 572
  }
]
```

#### Error Responses

| Status | When |
|--------|------|
| `400 Bad Request` | `n < 1` or `n > 500` |
| `503 Service Unavailable` | Hacker News API is unreachable |
| `500 Internal Server Error` | Unexpected server error |

All errors follow [RFC 7807 Problem Details](https://tools.ietf.org/html/rfc7807).

---

## Architecture

```
HackerNewsApi/
├── HackerNews.Api/                   ← ASP.NET Core host, controllers, middleware
│   ├── Controllers/
│   │   └── StoriesController.cs       ← Single endpoint: GET /api/stories/best
│   ├── Middleware/
│   │   └── GlobalExceptionMiddleware.cs
│   └── Program.cs
│
├── HackerNews.Application/           ← Business logic (no framework dependencies)
│   ├── Configuration/
│   │   └── StoryServiceSettings.cs
│   ├── DTOs/
│   │   ├── HackerNewsItemResponse.cs  ← Maps from HN Firebase API
│   │   └── StoryResponse.cs           ← Maps to our public API contract
│   ├── Interfaces/
│   │   ├── IHackerNewsClient.cs
│   │   └── IStoryService.cs
│   ├── Services/
│   │   └── StoryService.cs            ← Cache + parallel fetch + sort
│   └── DependencyInjection.cs
│
├── HackerNews.Infrastructure/         ← External concerns (HTTP, config)
│   ├── Clients/
│   │   └── HackerNewsClient.cs        ← Typed HttpClient for HN API
│   ├── Configuration/
│   │   └── HackerNewsClientSettings.cs
│   └── DependencyInjection.cs         ← Polly policies + HttpClientFactory
│
└── HackerNews.Tests/
    └── Services/
        ├── StoryServiceTests.cs
        └── StoriesControllerTests.cs
```

### Layer Responsibilities

| Layer | Responsibility | Dependencies |
|-------|---------------|--------------|
| **Api** | HTTP host, routing, input validation, middleware | Application, Infrastructure |
| **Application** | Business rules, caching strategy, orchestration | Abstractions only |
| **Infrastructure** | HttpClient, Polly policies, config binding | Application (interfaces) |

The dependency flow is **Api → Application ← Infrastructure**, following the Dependency Inversion Principle.

A Domain layer was intentionally omitted — this problem has no domain entities, invariants, or business rules that would justify one. The solution is an integration and orchestration problem, and adding Domain would be unnecessary complexity.

---

## Technical Decisions

### 1. Two-Tier In-Memory Cache

| What is cached | Key pattern | Default TTL | Why |
|----------------|-------------|-------------|-----|
| Best story IDs list | `best-story-ids` | 5 min | The list changes slowly; 1 request per TTL window instead of 1 per caller |
| Individual story details | `story-{id}` | 5 min | Scores update gradually; avoids re-fetching the same story for different values of n |

**Why not cache the final sorted array?** Because callers request variable `n`. Caching per-story
means a request for `n=5` and another for `n=50` share the same cached items instead of maintaining
separate caches for each `n`.

### 2. Controlled Parallelism (SemaphoreSlim)

Story details are fetched via `Task.WhenAll` for maximum throughput, but a `SemaphoreSlim`
(default: 20 slots) prevents bursting hundreds of concurrent HTTP requests to the HN API.
Cache hits bypass the semaphore entirely — they don't consume a slot.

A double-check pattern after acquiring the semaphore avoids redundant fetches when multiple
threads race for the same uncached story.

### 3. Polly Resilience Policies

| Policy | Configuration | Purpose |
|--------|---------------|---------|
| **Retry** | 3 attempts, exponential backoff + jitter | Handles transient 5xx / network blips |
| **Circuit Breaker** | Opens after 5 failures, 30s cooldown | Prevents cascading failures when HN API is fully down |

Jitter on retries prevents the "thundering herd" problem when the HN API recovers.

### 4. Typed HttpClient via HttpClientFactory

Avoids socket exhaustion (the classic `HttpClient` disposal trap) and provides a clean
integration point for Polly policies without polluting business logic.

### 5. Graceful Degradation

If a single story fails to load (404, timeout after retries), it is **skipped** rather than
failing the entire request. The caller gets `n - k` results where `k` is the number of
unrecoverable items. This is preferable to returning a 500 for one bad story out of 200.

### 6. Settings Separation

Configuration was split into `StoryServiceSettings` (Application layer) and `HackerNewsClientSettings`
(Infrastructure layer). Each layer reads only the properties it needs from the same `appsettings.json`
section, avoiding a circular dependency between Application and Infrastructure.

---

## Assumptions

1. **The HN `/beststories.json` list is roughly ordered by score** — the API returns up to 200 IDs
   in a quality-based order. We take the first `n` and re-sort by exact score.
2. **`n` has a sane upper limit** — capped at 500 to prevent abuse (configurable via
   `appsettings.json`).
3. **Stories without a URL are valid** — some HN posts (Ask HN, Show HN) have no external link.
   The `uri` field will be `null` in those cases.
4. **Eventual consistency is acceptable** — data may be up to 5 minutes stale due to caching. For
   a leaderboard use case, this is a reasonable trade-off vs. API overload.

---

## Possible Enhancements

Given more time, these would be the next improvements:

| Enhancement | Impact | Complexity |
|-------------|--------|------------|
| **Redis distributed cache** | Enables horizontal scaling (multiple API instances share cache) | Medium |
| **Background refresh (IHostedService)** | Pre-warms cache periodically so first requests are never cold | Low |
| **ETag / conditional requests** | Reduces bandwidth to HN API by only fetching changed items | Medium |
| **Rate limiting** | Protects our own API from abusive callers | Low |
| **Health checks** | `/health` endpoint that pings HN API for operational monitoring | Low |
| **Response compression** | Gzip/Brotli for large payloads when n is high | Low |
| **Integration tests** | `WebApplicationFactory<Program>` with a mocked HN API (WireMock) | Medium |
| **OpenTelemetry tracing** | Distributed tracing across cache hits/misses and HN API calls | Medium |
| **API versioning** | `/api/v1/stories/best` for backward-compatible evolution | Low |

---

## Technologies

- **.NET 10** / ASP.NET Core 10
- **Polly** — Retry + Circuit Breaker resilience
- **HttpClientFactory** — Managed HttpClient lifecycle
- **IMemoryCache** — In-process caching
- **Swagger / Swashbuckle** — API documentation
- **xUnit + Moq + FluentAssertions** — Testing