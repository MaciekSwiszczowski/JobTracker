# The JobTracker service API

The contract between the three parts that never see each other's code:

- **the Claude skill**, which writes: it turns a pasted job offer or a "they called me" note into JSON and posts it;
- **the service** (`src/JobTracker.Api`), which stores it in MongoDB;
- **the UI** (`src/JobTracker.UI`), which reads it and can close an application.

The C# shape is in `src/JobTracker.Contracts`. The examples in [`api/`](api) are real documents:
`tests/JobTracker.Contracts.Tests` deserializes every one of them, so an example that drifts from the records fails
the tests. Change the records, the examples and this page together.

## Basics

- Address: `http://localhost:5080`, loopback only. No authentication: it is a single-user service on your machine.
  The UI and the skill read `JOBTRACKER_API` to point elsewhere.
- JSON with camelCase names. Dates are `yyyy-MM-dd`; times are ISO 8601 with their offset
  (`2026-10-22T14:30:00+02:00`).
- A required field that is missing or `null` is a `400`. Unknown fields at the top level are ignored; put
  anything new in `extra` (or an event's `details`), which is stored exactly as sent.
- Errors are [problem details](https://www.rfc-editor.org/rfc/rfc9457) (`application/problem+json`). The UI shows
  `detail` when there is one, otherwise `title`, so write them for a person.

## Endpoints

| Method and route | Body | Success | Errors |
| --- | --- | --- | --- |
| `GET /health` | | `200` `{"status":"ok"}` | |
| `GET /applications` | | `200` array of summaries ([example](api/application-list.example.json)) | |
| `POST /applications` | new application ([example](api/new-application.example.json)) | `201`, `Location: /applications/{id}`, body `{"id":"…"}` | `400` invalid, `409` an open application for the same company and title exists |
| `GET /applications/{id}` | | `200` the whole document ([example](api/application-detail.example.json)) | `404` |
| `POST /applications/{id}/events` | one event ([example](api/event.example.json)) | `204` | `400`, `404` |
| `POST /applications/{id}/close` | closure ([example](api/closure.example.json)) | `204` | `400` (for example closed before it was applied), `404`, `409` already closed |

`GET /applications` returns every application; the UI sorts and filters. Paging and server-side filters come
later, when there is something to learn from them.

## The application document

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `company.name` | string | yes | As the posting writes it. |
| `company.website` | string | | The company's own site. |
| `title` | string | yes | The job title from the posting. |
| `appliedOn` | date | yes | The day the application was sent. |
| `description` | string | | The role description, in the posting's words. |
| `sourceUrl` | string | | Where the posting was found. |
| `extra` | object | | Everything else, any shape. See the conventional keys below. |
| `rawText` | string | | The pasted posting, verbatim. |

The service adds `id`, `status` (`open` or `closed`), `events` (oldest first) and `closure` (null while open).

### Conventional keys in `extra`

`extra` takes anything. These keys are the ones the skill uses when the information is there, so that the same
fact is stored under the same name in every document, which is what makes it queryable later:

| Key | Shape | Example |
| --- | --- | --- |
| `salary` | `{ min, max, currency, period, contract }` | `{ "min": 24000, "max": 30000, "currency": "PLN", "period": "month", "contract": "B2B" }` |
| `location` | string | `"Warsaw"` |
| `workMode` | `"remote"`, `"hybrid"` or `"onsite"` | `"hybrid"` |
| `officeDaysPerWeek` | number | `2` |
| `techStack` | array of strings | `["C#", "MongoDB"]` |
| `seniority` | string | `"senior"` |
| `employmentType` | string | `"B2B"`, `"permanent"`, `"contract"` |
| `languages` | array of strings | `["English", "Polish"]` |
| `recruiter` | `{ name, email, phone, agency }` | `{ "name": "Anna Nowak", "email": "anna.nowak@contoso.example" }` |
| `benefits` | array of strings | `["private healthcare", "Multisport"]` |
| `source` | string | where it was found: `"LinkedIn"`, `"justjoin.it"`, `"referral"` |

Anything else goes in under a new camelCase key. Numbers are JSON numbers, not strings; amounts are as the posting
gives them (no conversion), with `currency` as an ISO 4217 code.

## Events

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `at` | date-time with offset | yes | When it happened. |
| `type` | string | yes | camelCase; one of the types below where one fits. |
| `summary` | string | yes | One sentence for the timeline. |
| `details` | object | | Anything else, any shape. |

Event types the skill uses: `applied`, `recruiterCall`, `message`, `interviewScheduled`, `interview`,
`assignment`, `offer`, `rejection`, `followUp`, `note`. A new type is allowed when none of these fits.

## Closing

`{ "closedOn": "2026-10-28", "reason": "Rejected" }`. The reason is free text; the UI offers Rejected, Withdrawn,
No response, Accepted offer and Declined offer.
