# BranchBooking API guide

Base path: `/api/v1`. JSON in and out. The machine-readable description is served at `/openapi/v1.json` when
`OpenApi:Enabled` is true (on in Development, off by default elsewhere).

## Endpoints

| Method and path | Purpose | Success |
|---|---|---|
| `GET /appointment-types` | Active appointment types | 200 |
| `GET /branches` | Active branches with opening hours | 200 |
| `GET /branches/{id}/availability?date=YYYY-MM-DD` | Slots for a branch-local date with places remaining | 200 |
| `POST /bookings` | Create a booking | 201 + `Location` |
| `GET /bookings/{reference}` | Look up a booking | 200 |
| `POST /bookings/{reference}/cancel` | Cancel (body optional: `{"reason": "..."}`) | 200 |
| `POST /bookings/{reference}/reschedule` | Move to another time: `{"newStartsAtUtc": "..."}` | 200 |
| `GET /health`, `GET /health/ready` | Liveness and readiness | 200 |

A booking reference such as `BK-7H2QX-M9RTD` is the only credential. It is accepted in any casing and with or without
hyphens. Booking responses show a masked email and phone and are sent with `Cache-Control: no-store`.

## Errors

Every error is `application/problem+json` with `status`, `title`, `detail`, a stable `code`, `traceId` and, when
available, `correlationId`. Clients should branch on `code`, never on the wording.

| Status | `code` | When |
|---|---|---|
| 400 | `validation_failed` | A field is missing or malformed (the `errors` member lists them) |
| 400 | `invalid_customer_details` | Name, email or phone rejected |
| 400 | `invalid_cancellation_reason` | Reason longer than 500 characters |
| 400 | `date_outside_booking_window` | Date is in the past or beyond the booking horizon |
| 400 | `slot_not_offered` | The time is not a slot the branch offers |
| 400 | `same_slot` | Reschedule to the time the booking already has |
| 404 | `branch_not_found` | Unknown or inactive branch |
| 404 | `appointment_type_not_found` | Unknown or inactive appointment type |
| 404 | `booking_not_found` | Unknown or malformed reference (deliberately indistinguishable) |
| 409 | `slot_not_available` | The slot is full |
| 409 | `booking_not_active` | The booking is cancelled |
| 409 | `concurrent_change` | The booking changed at the same moment; try again |
| 422 | `slot_too_soon` | The slot starts inside the minimum notice |
| 422 | `change_window_closed` | Cancel or reschedule is too close to the start |
| 422 | `reschedule_limit_reached` | The booking has used its reschedules |
| 429 | `rate_limited` | Too many requests; see `Retry-After` |
| 500 | `internal_error` | Unexpected failure; quote `correlationId` |

`ErrorCatalogueTests` fails when an error code is added or removed without updating its expected list; update this
table in the same change.

## Business settings

| Setting | Default | Meaning |
|---|---|---|
| `BookingPolicy:MinimumNoticeMinutes` | 60 | A booking or reschedule must start at least this far ahead |
| `BookingPolicy:MaxAdvanceDays` | 30 | Booking horizon, counted on the branch's calendar |
| `BookingPolicy:ChangeCutoffHours` | 2 | Cancel and reschedule close this long before the start |
| `BookingPolicy:MaxReschedules` | 3 | Reschedules per booking |

These defaults are planner choices awaiting business confirmation.

## Rate limits (per client address, per minute)

| Setting | Default | Applies to |
|---|---|---|
| `RateLimiting:WritePermitsPerMinute` | 10 | create, cancel, reschedule |
| `RateLimiting:LookupPermitsPerMinute` | 30 | lookup by reference |
| `RateLimiting:GlobalPermitsPerMinute` | 300 | everything except health |
| `RateLimiting:Enabled` | true | set false only for load tests |

Behind nginx, set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` so the limits see the customer's address, and do not
publish the API's own port. Without it every request appears to come from the proxy and shares one limit.

## CORS

None by default: the web app and API share an origin. To allow a separate web origin list it exactly, for example
`Cors__AllowedOrigins__0=https://book.example.com`. Wildcards are refused at startup.

## Logging and privacy

Request logs contain the method, the route template (`api/v1/bookings/{reference}`), the status and the duration, plus a
correlation id. They never contain a reference, a name, an email address or a phone number. Keep it that way: log
internal booking ids only. The default ASP.NET and EF Core request/command logs are set to `Warning` for this reason.
Reverse proxies log URLs too; WI-105's nginx configuration redacts references from its access log.
