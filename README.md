# Hacker News Best Stories API

A small ASP.NET Core API that returns the best n Hacker News stories, ranked by score,
using the public Hacker News API as the source of truth.

## What it does

`GET /api/beststories/{n}` returns the top `n` stories from Hacker News' "best stories"
list, sorted by score from highest to lowest, in this shape:

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

Try it locally with, for example, `GET /api/beststories/10` or `GET /api/beststories/50`.

## Running it

You'll need the .NET 8 SDK installed.

```bash
git clone https://github.com/kkurapaty/HackerNewsBestStories.git
cd HackerNewsBestStories
dotnet test
dotnet run --project src/HackerNewsBestStories.Api
```

The API comes up on `http://localhost:5138` by default (see `launchSettings.json`), with
Swagger UI `http://localhost:5138/Swagger/index.html` at the root so you can try it in a browser without any tooling.

If you'd rather not install the SDK, there's a Dockerfile:

```bash
docker build -t hn-best-stories .
docker run -p 5138:5138 hn-best-stories
```

Then hit `http://localhost:5138/api/beststories/10`.

## How it's put together

It's one API project plus one test project, no more layers than the problem calls for:

- `Controllers/BestStoriesController.cs` - the single endpoint, does input validation and
  nothing else.
- `Services/HackerNewsClient.cs` - talks to the real Hacker News API over HTTP.
- `Services/HackerNewsService.cs` - the actual logic: get the id list, fetch story details,
  filter, sort by score, take n, map to the response shape. This is the class most of the
  unit tests are aimed at, since it's where the interesting behaviour lives.
- `Infrastructure/KeyedLock.cs` - a small helper used to stop duplicate concurrent fetches
  of the same uncached data (see below).

`IHackerNewsClient` sits between the service and the real HTTP calls so the service's
logic can be tested without a single network call. The tests use Moq for that boundary
and a real `IMemoryCache` everywhere else, since faking a cache tends to just re-implement
a worse cache.

## Not overloading the Hacker News API

This was the part of the brief I spent the most thought on, since it's easy to write
something that technically works but falls over, or falls over the *upstream API*, the
moment real traffic hits it. Three things are doing the work here:

1. **Caching.** The list of best story ids is cached for a short window (60 seconds by
   default), and each story's details are cached separately (120 seconds by default).
   Story rankings don't change second to second, so there is no reason to ask Hacker
   News the same question a hundred times a minute. Both are configurable in
   `appsettings.json` under `HackerNewsApi`.

2. **Stampede protection.** A plain `IMemoryCache.GetOrCreate` still lets every concurrent
   request through on a cache miss, so a burst of 50 requests arriving at once on a cold
   cache would fire 50 identical calls at Hacker News. `KeyedLock` hands out one lock per
   cache key, so only the first caller for a given id actually goes to the network, and
   everyone else waits a moment and then reads what it fetched.

3. **Bounded concurrency and resilience for the fan-out.** A single call to our endpoint
   can mean fetching up to a couple hundred story ids from Hacker News. Those fetches run
   concurrently for speed, but capped at 20 in flight at once (`MaxConcurrentItemFetches`),
   rather than opening one request per story simultaneously. On top of that, the outbound
   `HttpClient` is wrapped with `AddStandardResilienceHandler()`, which gives us retries
   with backoff, a per-call timeout, and a circuit breaker, all from the
   `Microsoft.Extensions.Http.Resilience` package rather than anything hand rolled. If
   Hacker News is genuinely down, we stop hammering it and return a 502 to our own callers
   instead of hanging or retrying forever.

## Assumptions I made

- **"Best stories" means whatever `/v0/beststories.json` currently returns.** That list
  isn't guaranteed to be sorted by score (and in practice isn't reliably), so the service
  fetches details for everything in it and sorts by score itself, rather than trusting the
  order of the id list.
- **`n` larger than the number of available stories just returns what's available.** The
  brief doesn't say to error in that case, and failing a request because the caller asked
  for more than currently exists felt unhelpful.
- **`n` of zero or negative, or anything that isn't a whole number, is a 400.** I parse `n`
  by hand rather than using a route constraint like `{n:int}`, because a route constraint
  mismatch gives a bare 404 with no explanation, and a 400 with a message is a much better
  experience for whoever's calling this.
- **Deleted, dead, and non-story items (jobs, polls) are silently dropped**, not counted
  towards `n`. A caller asking for the best 10 stories almost certainly doesn't want a job
  posting or a dead item taking one of those ten slots.
- **A story with no URL (an "Ask HN" or similar text post) links to the Hacker News
  discussion page instead.** Returning an empty string for `uri` felt like it would just
  push the broken-link problem onto whoever consumes this API.
- **A story that fails to load, or comes back as one of the excluded types above, is
  dropped rather than failing the whole request.** One bad id out of two hundred shouldn't
  take down the response.
- I've gone with .NET 8, since it's the current LTS release and the safest choice for
  something meant to be picked up and run by someone else.

## What I'd do differently with more time

- **Swap `IMemoryCache` for a distributed cache (Redis) if this ran on more than one
  instance.** Right now, each instance keeps its own cache and its own locks, so a
  cold start on a freshly scaled-out instance still causes a burst of calls to Hacker
  News. Fine for one box, not ideal for a fleet.
- **Add response caching / output caching at the API layer itself**, so identical requests
  for the same `n` within a short window don't even reach the service layer. Small win on
  top of the internal caching that's already there.
- **Add a health check endpoint** that pings the Hacker News API, so this can be plugged
  into standard container orchestration health probes.
- **Add structured logging and basic metrics** (request counts, cache hit rate, upstream
  latency) rather than the plain `ILogger` warning I've got now. Fine for a take home
  exercise, not something I'd ship to production as is.
- **Consider ETags or a `Last-Modified` header** on the response, since the underlying data
  changes slowly enough that a lot of callers could skip re-downloading the body entirely.
- **Rate limit the API itself** (ASP.NET Core's built in rate limiting middleware would do
  the job) so one aggressive caller of *our* API can't do the same thing to us that we're
  trying to avoid doing to Hacker News.

## Tests

`dotnet test` runs everything. There are three groups:

- `HackerNewsServiceTests` - the ranking, filtering, field mapping and caching behaviour,
  with the Hacker News client mocked out.
- `BestStoriesControllerTests` - input validation and that the controller correctly hands
  off to the service.
- `BestStoriesEndpointTests` - boots the real ASP.NET Core pipeline with
  `WebApplicationFactory` and a fake client swapped in, as a sanity check that the DI
  wiring in `Program.cs` actually works end to end, not just each piece in isolation.
