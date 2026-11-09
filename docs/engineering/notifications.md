# Notifications

## How it works

1. Booking, cancelling and rescheduling each write a row to `notification_messages` in the same transaction as the
   change (an outbox). The API never sends email.
2. The Worker's dispatcher claims due rows every `PollSeconds`, writes the email from the booking as it is now,
   sends it through the configured channel and records the result on the row.
3. Reminders are queued by the Worker (see Reminders below) and travel the same path.

| Status | Meaning |
|---|---|
| `Pending` | Waiting. With `attempts` > 0 it is waiting for a retry at `next_attempt_at_utc`. |
| `Sent` | Accepted by the channel. `channel` and `provider_message_id` are set. |
| `Skipped` | No longer true when its turn came. `detail` says why (below). |
| `Failed` | Gave up. `detail` is a short code such as `smtp_550`. |

| Skip reason (`detail`) | Meaning |
|---|---|
| `booking_not_confirmed` | The booking was cancelled before the message was sent |
| `booking_not_cancelled` | A cancellation message for a booking that is not cancelled |
| `superseded` | The booking moved; a newer message covers the new time |
| `appointment_passed` | The appointment time has gone |

## Channels

- **Simulated (default).** Sends nothing. The rendered subject and body are kept on the row and shown by
  `GET /api/v1/bookings/{reference}/messages`. Use it for development, demos and automated tests.
- **Smtp.** Email through an SMTP server: Mailpit for development and demos, or any real server. Only the message's
  `Message-Id` is kept; the text is not stored in the database.

### Seeing the emails with Mailpit

Mailpit is a development mail catcher. It accepts every message, shows it in a web page and delivers nothing to
anyone, so any address works and no real person is ever emailed. It needs no account, key or verified sender.
**Do not point production at it:** a message it accepts is recorded as `Sent` although nobody receives it.

1. In `.env` set `NOTIFICATIONS_CHANNEL=Smtp`. The `SMTP_*` defaults already point at the `mailpit` service.
2. Start the stack with the `mail` profile (add `--build` the first time):

   ```bash
   docker compose --profile mail up -d
   ```

   To always include it, put `COMPOSE_PROFILES=mail` in `.env`.
3. Book an appointment. Within about `PollSeconds` the message is `Sent` with channel `Smtp`, and the email is in the inbox at
   <http://localhost:8025> with the reference, branch, local time and link. The HTML and plain-text versions are both there.
4. Without Docker, run Mailpit as a program (it is a single executable; see its documentation) and start the Worker with
   `Notifications__Channel=Smtp`, `Notifications__Smtp__Host=localhost`, `Notifications__Smtp__Port=1025`,
   `Notifications__Smtp__Security=None` and a `Notifications__Smtp__SenderEmail`. Mailpit listens for SMTP on port 1025
   and serves its web page on port 8025 unless told otherwise.

### Using a real SMTP server

Set the host, port, security (`StartTls` for port 587, `SslOnConnect` for port 465), the username and password the server
gave you, and a sender address you are allowed to send as. The Worker refuses to start when the channel is `Smtp` and a
setting is missing or inconsistent, and the error names `Notifications:Smtp` without printing the password.
A username without a password (or the reverse) is refused, and so is a username with `Security=None`, so a
password is never sent unencrypted. Use `dotnet user-secrets` on the Worker project or your own `.env`; never a committed file.

Mail from a sender address the server does not trust is refused (permanent `smtp_5xx` codes), and many receiving servers
treat mail from a brand you do not own as spam.

### What the codes mean

| `detail` | Meaning | Retried? |
|---|---|---|
| `smtp_421`, `smtp_450`, `smtp_451`, `smtp_452` (any 4xx) | The server asked us to try later (busy, greylisting, storage, shutting down) | Yes |
| `smtp_550`, `smtp_553`, `smtp_554` (any 5xx) | The server refused: unknown mailbox, sender not allowed, rejected as spam | No |
| `smtp_error` | A reply outside 400 to 599 where a failure was expected | No |
| `smtp_auth_failed` | The username or password was refused | No |
| `smtp_tls_failed` | The TLS handshake failed: wrong `Security` for the port, or an untrusted certificate | No |
| `smtp_protocol_error` | The server answered in a way the client could not follow | Yes |
| `invalid_recipient` | The customer's address cannot be used as an email address | No |
| `sender_invalid` | `SenderEmail` is not an address (normally caught at start-up) | No |
| `timeout`, `network_error` | No answer within `TimeoutSeconds`, or no connection (for Mailpit: it is not running) | Yes |

The server's own reply text is never stored, because it can contain the recipient's address.

## Settings (`Notifications` section; environment variables use `__`)

| Setting | Default | Meaning |
|---|---|---|
| `Channel` | `Simulated` | `Simulated` or `Smtp` |
| `BrandName` | `BranchBooking` | Name in the message footer |
| `PublicBaseUrl` | `http://localhost:8080` | Link base in messages |
| `ManagePath` | `/manage` | Page for viewing and changing a booking |
| `BatchSize` | 20 | Messages per pass |
| `PollSeconds` | 10 | Seconds between passes |
| `MaxAttempts` | 5 | Attempts before `Failed` |
| `RetryBaseSeconds` | 30 | Delay after the first failure; doubles each time |
| `RetryMaxSeconds` | 3600 | Cap on the delay |
| `LeaseSeconds` | 300 | How long a claimed message is reserved; keep it longer than a whole batch of sends |

| Setting (`Notifications:Smtp`) | Default | Meaning |
|---|---|---|
| `Host` | none | SMTP server name. `mailpit` in compose, `localhost` outside Docker |
| `Port` | 587 | 1025 for Mailpit |
| `Security` | `StartTls` | `None`, `StartTls` or `SslOnConnect`. `None` for Mailpit only |
| `Username` | empty | Leave empty for Mailpit |
| `Password` | empty | Secret. Environment or user-secrets only |
| `SenderEmail` | none | The From address |
| `SenderName` | `BranchBooking` | Display name |
| `TimeoutSeconds` | 10 | For the whole send (connect, log in, transmit) |

The Worker refuses to start when a value is out of range.

## Checking on it

```sql
-- Backlog and outcomes
select status, count(*) from notification_messages group by status;

-- What is waiting longest
select kind, attempts, next_attempt_at_utc, detail
from notification_messages where status = 'Pending' order by next_attempt_at_utc limit 20;

-- Recent failures
select kind, attempts, detail, created_at_utc
from notification_messages where status = 'Failed' order by created_at_utc desc limit 20;
```

## Putting failed messages back in the queue

After fixing the cause (for example a wrong API key), release failed messages for another round of attempts:

```sql
update notification_messages
set status = 'Pending', attempts = 0, next_attempt_at_utc = now(), locked_until_utc = null, detail = null
where status = 'Failed' and created_at_utc > now() - interval '2 days';
```

Messages whose appointment has passed are skipped when they are next claimed.

## Privacy

Logs and `detail` never contain a reference, a name, an email address or message text. The simulated channel keeps the
message text (which includes the reference and the customer's name) in the database on purpose; real channels do not.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| Everything stays `Pending` | The Worker is not running, cannot reach the database, or `Notifications` settings are invalid (see its log) |
| `Pending` with `attempts` > 0 | The channel is failing; read `detail` and the Worker log |
| Worker log says a time zone is missing | The container image lacks tzdata; install it in the Worker image |
| A message was sent twice | The Worker stopped between the provider accepting it and saving the result; rare and by design (at-least-once) |


## Reminders

A second Worker loop looks every `ScanIntervalSeconds` for confirmed bookings whose appointment is within `LeadHours`
(but more than `MinimumLeadMinutes` away) and that have not changed for `MinimumGapHours`, and queues one `Reminder`
message for each. The message is sent by the same dispatcher as every other message.

| Setting (`Notifications:Reminders`) | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | `false` stops the scan (env `REMINDERS_ENABLED`) |
| `LeadHours` | 24 | Remind once the appointment is within this many hours |
| `MinimumLeadMinutes` | 60 | Do not remind when the appointment is this close or closer |
| `MinimumGapHours` | 4 | A booking must be untouched this long, so a reminder never trails a fresh confirmation |
| `ScanIntervalSeconds` | 60 | Seconds between scans |
| `BatchSize` | 100 | Reminders queued per round |

What to expect:

- A booking made more than 24 hours ahead is reminded when it reaches 24 hours to go.
- A booking made 26 hours ahead is reminded four hours after it was made (22 hours to go), not immediately.
- A booking made less than four hours before the appointment gets no reminder; its confirmation is enough.
- A rescheduled booking is reminded again for its new time. An unsent reminder for the old time is skipped
  (`superseded`).
- A cancelled booking gets no reminder.
- Reminders cannot be sent twice for the same appointment time: the database refuses a second one.

```sql
-- Reminders and what became of them
select status, count(*) from notification_messages where kind = 'Reminder' group by status;
```
