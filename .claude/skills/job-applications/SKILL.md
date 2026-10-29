---
name: job-applications
description: Use when the user pastes a job offer or says they applied for a job, when they report news about an application (a recruiter called, an interview, a task, an offer, a rejection), or when they want an application closed. Turns it into JobTracker JSON and posts it to the local JobTracker service.
---

# Recording job applications in JobTracker

The user tracks the jobs they apply for in JobTracker. You are its only way in: you read what they paste, turn it
into the JSON the service expects, and post it. The UI only reads.

**Before the first post in a session, read [`docs/api.md`](../../../docs/api.md) and the examples in
[`docs/api/`](../../../docs/api).** They are the contract and the tests hold them to the code. This file says how
to work; that page says what to send. Where the two disagree, `docs/api.md` wins.

The service is at `$JOBTRACKER_API`, or `http://localhost:5080` when that is not set.

## What the user asks, and what you do

| The user… | You… |
| --- | --- |
| pastes a job offer, or says "I applied for this" | create an application (below) |
| says something happened: "Contoso called", "interview on Tuesday", "got the task", "they rejected me" | add an event; for a rejection or an accepted offer, also offer to close it |
| says "close Contoso", "I withdrew from Fabrikam" | close the application |
| says "send what's in the inbox" | post every file in `inbox/`, oldest first (see "When the service is down") |

## Creating an application

1. **Extract** into the new-application shape from `docs/api.md`:
   - `company.name` and `title` are required. If you cannot tell either from the text, ask; never invent one.
   - `appliedOn` is today (`date +%F`) unless the user says otherwise. Say which date you used.
   - `description`: the role description, in the posting's own words, shortened only by dropping boilerplate.
   - `sourceUrl`: only a URL the user gave you or that appears in the text.
   - `extra`: every other fact the posting states, under the conventional keys in `docs/api.md` where one fits
     (`salary`, `location`, `workMode`, `techStack`, `recruiter`, …) and a new camelCase key otherwise. Numbers as
     numbers. Leave a key out rather than guess or write `null`.
   - `rawText`: the pasted text exactly as given. It is the safety net for anything you missed.
2. **Check for a duplicate**: `GET /applications` and look for an open application with the same company and a
   similar title. If there is one, show it and ask whether this is new or an update.
3. **Write the JSON to a file** in `inbox/`, named `<appliedOn>-<company>-<title>.json` in lower-case-with-dashes,
   and post the file. A file rather than inline JSON, so that quoting cannot break it and nothing is lost if the
   post fails:

   ```bash
   curl -sS -w '\n%{http_code}\n' -X POST -H 'Content-Type: application/json' \
     --data-binary @inbox/2026-10-20-contoso-senior-net-developer.json \
     "${JOBTRACKER_API:-http://localhost:5080}/applications"
   ```

4. **On `201`**, move the file to `inbox/sent/` and report in one line: company, title, the new id, and how many
   extra properties were stored. On `400` or `409`, show the problem's `detail` and fix or ask; do not retry
   blindly.

## Adding an event

1. `GET /applications`, and find the application by company (and title when the company has several). If more
   than one matches, ask which.
2. Build the event: `at` is when it happened, with the local offset (`date --iso-8601=seconds` for "now"; 12:00
   local time when only the day is known). `type` from the list in `docs/api.md`; `summary` is one sentence;
   anything else (interview time, format, people, salary offered, deadline) goes into `details`.
3. `POST /applications/{id}/events` with the JSON in a file, as above. `204` is success.

## Closing

`POST /applications/{id}/close` with `{ "closedOn": "<day>", "reason": "<a few words>" }`. Use the reasons the UI
offers when one fits: Rejected, Withdrawn, No response, Accepted offer, Declined offer. `409` means it is
already closed: say so and stop.

## When the service is down

Check with `curl -sS "${JOBTRACKER_API:-http://localhost:5080}/health"`. If the connection is refused:

- **A new application**: keep the file in `inbox/`, tell the user it is queued, and that the service starts with
  `dotnet run --project src/JobTracker.Api` (after `docker compose up -d` for MongoDB). When asked to send the
  inbox later, post each file oldest first and move each one to `inbox/sent/` on `201`.
- **An event or a closure** needs an id, so it cannot be queued. Say so, and give the user the JSON to post later.

Never start the service or Docker yourself unless the user asks.

## Rules

- Never post anything the text does not support. A guessed salary or an invented recruiter is worse than none.
- Keep the user's language in `summary` and `description`; keys stay English camelCase.
- One application per job. Two roles in one posting are two applications; ask first.
- `inbox/` is git-ignored: it holds personal data and stays on this machine.
