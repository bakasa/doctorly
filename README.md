# Doctorly - Practice Calendar API

Backend API for managing a doctor's practice calendar: events, attendees, RSVPs.
Built for the Doctorly technical test. No frontend, per the brief.

## Quick start

```bash
docker compose up -d db
dotnet run --project src/Doctorly.Api
```

That's it. Migrations run automatically on startup. Postgres is exposed on host port
**55432**, not 5432 (something else on the dev machine already owned 5432 - see
Assumptions).

- API: `http://localhost:5080`
- Interactive docs: `http://localhost:5080/scalar`
- OpenAPI document: `http://localhost:5080/openapi/v1.json`

Run the tests:

```bash
dotnet test tests/Doctorly.Domain.Tests          # pure, no infra
dotnet test tests/Doctorly.Application.Tests     # Moq-based, no infra
docker compose up -d db
dotnet test tests/Doctorly.Api.Tests             # persistence + HTTP endpoint tests, needs the database running
```

## Architecture

Four layers, dependencies flow one direction (Domain has none):

```
Doctorly.Api             composition root - Minimal API endpoints, DI wiring, OpenAPI/Scalar
    |
    v
Doctorly.Infrastructure   EF Core, Postgres, migrations, console notifier
    |
    v
Doctorly.Application      use cases (EventsAppService), abstractions, DTOs
    |
    v
Doctorly.Domain           Event aggregate, Attendee, value objects, domain events - no dependencies
```

`Doctorly.Client` is a generated C# HTTP client, not part of the layering above - see
"API client" below.

`Event` is the aggregate root. `Attendee` only exists inside it - there's no
`IAttendeeRepository`, every write goes through `Event`. Field-size limits (Title <=200,
Description <=2000, Attendee.Name <=100, Email <=254) and invariants (End > Start) are
enforced once, in domain constructors and value objects, not duplicated at the API
boundary. A `DomainException` from any of those maps to `400` at the edge.

## API reference

Resource-oriented REST, versioned under `/api/v1`:

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/v1/events` | Create event (+ initial attendees) |
| GET | `/api/v1/events` | List/search/filter |
| GET | `/api/v1/events/{id}` | Get one event (returns `ETag`) |
| PUT | `/api/v1/events/{id}` | Update title/description/time (requires `If-Match`) |
| DELETE | `/api/v1/events/{id}` | Cancel event |
| POST | `/api/v1/events/{eventId}/attendees` | Add an attendee |
| PATCH | `/api/v1/events/{eventId}/attendees/{attendeeId}` | Accept/reject (RSVP) |

### Walkthrough

Create an event:

```bash
curl -X POST http://localhost:5080/api/v1/events \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Checkup",
    "description": "Routine checkup",
    "start": "2026-08-22T08:00:00+02:00",
    "end": "2026-08-22T08:30:00+02:00",
    "attendees": [{"name": "Jane Doe", "email": "jane@example.com"}]
  }'
# -> 201, ETag: "1", stored as UTC regardless of the offset sent
```

Filter/search:

```bash
curl "http://localhost:5080/api/v1/events?search=Checkup&attendeeEmail=jane@example.com"
curl "http://localhost:5080/api/v1/events?status=Scheduled&from=2026-08-22T00:00:00Z&to=2026-08-23T00:00:00Z"
```

Update, with optimistic concurrency:

```bash
ID=<event id>
curl http://localhost:5080/api/v1/events/$ID -i   # note the ETag, say "1"

# stale If-Match -> 412
curl -i -X PUT http://localhost:5080/api/v1/events/$ID \
  -H "Content-Type: application/json" -H 'If-Match: "999"' \
  -d '{"title":"Checkup (updated)","description":"desc","start":"2026-08-22T09:00:00+02:00","end":"2026-08-22T09:45:00+02:00"}'
# -> 412 Precondition Failed

# correct If-Match -> 200, new ETag
curl -i -X PUT http://localhost:5080/api/v1/events/$ID \
  -H "Content-Type: application/json" -H 'If-Match: "1"' \
  -d '{"title":"Checkup (updated)","description":"desc","start":"2026-08-22T09:00:00+02:00","end":"2026-08-22T09:45:00+02:00"}'
# -> 200, ETag: "2"
```

RSVP and add an attendee:

```bash
curl -X PATCH http://localhost:5080/api/v1/events/$ID/attendees/<attendee id> \
  -H "Content-Type: application/json" -d '{"isAttending": true}'

curl -X POST http://localhost:5080/api/v1/events/$ID/attendees \
  -H "Content-Type: application/json" -d '{"name":"Extra Person","email":"extra@example.com"}'
```

Cancel (soft delete):

```bash
curl -X DELETE http://localhost:5080/api/v1/events/$ID   # -> 204
curl http://localhost:5080/api/v1/events/$ID              # still 200, status "Cancelled"
```

## Design decisions

### Concurrency: two different problems, two different answers

The brief's "Could" question asks how to deal with updates to the same event, and
separately stresses that preserving data is important. Those are two different problems.

**Same-event concurrent updates.** `Event.Version` is a domain-owned `int`, incremented on
every mutating method, mapped as an EF Core concurrency token. It's exposed over HTTP as a
standard `ETag`/`If-Match` pair rather than a Postgres-specific mechanism like `xmin` -
`Version` is visible on the aggregate itself and round-trips through `EventDto` without a
database concept crossing the Application boundary. `PUT` reads `If-Match`, compares it
against the loaded entity's `Version` before mutating, and returns `412` on mismatch. A
race that slips past that check (two requests both pass the check, then both save) still
hits EF's own concurrency check on `SaveChanges` and surfaces as `409`.

**Preserving data.** Every commit that changes an `Event` also writes an append-only
`EventRevision` row - a JSON snapshot of the event's shape at that version, with a
timestamp. Not full event sourcing (no replay, no rebuilding state from revisions) - just
a queryable history of every version an event has been in, which is what "preservation of
data is of great importance" is actually asking for without the cost of a full
event-sourced system.

### Notifications

`Event` raises domain events (`EventCreated`, `EventUpdated`, `EventCancelled`,
`AttendeeAdded`, `AttendeeResponded`). After a commit succeeds, a small dispatcher forwards
them to `INotificationService`. The only implementation is a console/log notifier - it logs
what would have been sent, to whom, without needing SMTP credentials or a broker running to
demo the feature. The seam is the point: swapping in real email, iCal generation, or a
queue publisher means writing one new `INotificationService` implementation, nothing else
changes. No MediatR - there's one dispatch path and one consumer, so a full mediator
library would be an abstraction with nothing to abstract yet.

### DELETE cancels, doesn't remove

Given the brief's emphasis on preserving data, `DELETE /api/v1/events/{id}` calls the
domain's `Cancel()` - status becomes `Cancelled`, the row stays, and it gets its own
`EventRevision`. `GET` after a `DELETE` still returns `200` with `"status": "Cancelled"`,
not `404`.

### PUT doesn't touch attendees

Replacing the attendee list on every `PUT` would silently discard RSVP state
(`isAttending`) any time the title or time changed. `PUT` only updates
title/description/start/end. Attendees are managed through the nested `/attendees`
resource - `POST` to add one, `PATCH` to RSVP.

### List and search share one endpoint

`GET /api/v1/events` takes `from`, `to`, `status`, `attendeeEmail`, `search`, `page`,
`pageSize` as query parameters instead of a separate `/search` route - filtering and
searching are both just narrowing the same result set, and a REST resource shouldn't grow
a second endpoint for that.

### Modifying a cancelled event returns 400, not 409

`Update`, `AddAttendee`, and `RespondAttendee` all throw a `DomainException` ("Cannot
modify a cancelled event") if called on a cancelled event, which maps to `400`. `409
Conflict` would also be a defensible status here (the request conflicts with the resource's
current state) - `400` was chosen because this is a client-side validation failure in the
same family as the other domain guard clauses (title too long, end before start), not a
concurrency race, and it keeps all domain-rule violations mapping to one status code.

## Assumptions

- **.NET 9, not .NET 5 or .NET 10.** The brief says .NET 5; the accompanying email says not
  to mind that requirement and to use the latest. Only the .NET 9 SDK is installed on the
  machine this was built on, so "latest" here means .NET 9 (LTS), not .NET 10.
- **Postgres on host port 55432.** Port 5432 was already bound by something else on the
  dev machine; remapped in `docker-compose.yml` rather than fighting for the default port.
- **Time zones normalize to UTC.** `TimeRange` converts both `Start`/`End` to UTC in its
  constructor. Postgres `timestamptz` (via Npgsql) only accepts a `DateTimeOffset` with a
  zero offset - this also means the API is offset-agnostic on the way in and always
  UTC-normalized on the way out.
- **Attendee identity is by email within an event**, not globally unique - two different
  events can each have an attendee with the same email address; they're separate
  `Attendee` rows.
- **No auth.** Out of scope for a 4-hour test with no auth requirement in the brief.
  Every write would need an authenticated actor in a real deployment (who created the
  event, who can cancel it) - noted as a gap, not solved here.

## Must / Should / Could coverage

| Requirement | Status |
|---|---|
| Attendees: Name, Email, Attending | Done |
| Events: Title, Description, Attendees, Start/End | Done |
| Sensible field size limits | Done - enforced in the domain |
| Notifications capability | Done - console/log implementation, real Email/iCal/MQ is a swap-in |
| Appropriate testing | Done - 25 domain + 7 application + 8 Postgres-backed (3 persistence, 5 HTTP endpoint) tests |
| OpenAPI specification | Done - `/openapi/v1.json` |
| Auto-generated client | Done - `src/Doctorly.Client`, see below |
| Public-facing auto-generated documentation | Done - Scalar at `/scalar` |
| Create / Update / Delete / List (filters) / Search | Done |
| Accept/Reject event | Done - `PATCH` on the attendee resource |
| Same-event update handling | Done - see Design decisions above |
| Attendee availability checking | Not built - `TimeRange.Overlaps()` exists on the domain value object as the building block, but there's no endpoint using it |

## API client

`src/Doctorly.Client` is a generated typed C# HTTP client (`DoctorlyClient`), built with
[NSwag](https://github.com/RicoSuter/NSwag) from `openapi.json` - a checked-in snapshot of
the OpenAPI document, so regenerating doesn't need the API running:

```bash
dotnet tool install -g NSwag.ConsoleCore
nswag run nswag.json
```

To refresh the snapshot from a running instance first: `curl http://localhost:5080/openapi/v1.json -o openapi.json`.
Any OpenAPI-compatible generator (`openapi-generator`, `kiota`, etc.) works the same way
against the same document, for non-.NET consumers too.

## Next steps

With more time, in priority order:
1. Real notification delivery (email via SMTP, or an `.ics` attachment) behind the existing
   `INotificationService` seam.
2. Attendee availability checking as an actual endpoint, using `TimeRange.Overlaps()`.
3. Auth - who can create/update/cancel an event.
4. Swap the shared dev Postgres in integration tests for an ephemeral Testcontainers
   instance, so tests don't depend on a long-lived local container.
