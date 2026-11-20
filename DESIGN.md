# Keyboard Key Remapping Service: Design

Last updated: 2026-11-19

This document is the design of record. When a decision changes, the relevant section is updated in place and the change is recorded in the [Change log](#change-log).

## Summary

The service is one ASP.NET Core Web API project with three endpoints on a single resource, backed by a SQLite file through `Microsoft.Data.Sqlite`. The keyboard is addressed by its name, `Apex Pro Gen 3`.

- `PATCH /api/keyboards/{keyboardName}/mappings` validates a list of remaps and applies them on top of the stored set in one transaction. Keys not in the list keep their current mapping. A key mapped to itself returns to its default.
- `DELETE /api/keyboards/{keyboardName}/mappings` resets every key to its default.
- `GET /api/keyboards/{keyboardName}/mappings` returns all 92 keys, each with the code it currently emits.
- The key catalogue is seeded into SQLite, and only keys a client has set are stored. A key with no stored mapping falls back to itself at read time.

The decision that shapes everything else is that assign means **merge**, not replace. It is decision B1 below.

## Requirements and scope

There are six requirements; everything else in this document is a design choice.

| Requirement | How the design meets it |
| --- | --- |
| Assign key mappings for a keyboard, with validation | `PATCH` endpoint, all-or-nothing validation, `204` on success |
| Get all mappings, including unmapped keys | `GET` endpoint returning all 92 keys with a `remapped` flag |
| Web API over HTTP | ASP.NET Core, JSON bodies |
| Object-oriented language, C# or Go preferred | C# on .NET 10 (LTS), layered controller, service, repository |
| SQLite storage that persists between runs | File database, schema and seed applied at startup |
| Runnable locally, no licensed packages | Only MIT and Apache-2.0 packages; `dotnet run` with no other setup |

The keyboard is the Apex Pro Gen 3. Its keys are the 92 listed in [`HidKeyCodes.md`](HidKeyCodes.md), each identified by an 8-bit USB HID usage code.

Everything beyond these six requirements is out of scope, except the reset endpoint, which merge needs (B1). The logical extensions are listed under [Next steps](#next-steps-out-of-scope).

## Assumptions and open questions

The requirements leave five points open. Each is resolved below with the reading taken and the cost of changing it later.

| Assumption | If it is wrong, the change is |
| --- | --- |
| Assign merges into the current set (B1) | Add `PUT` for whole-set replace; storage and validator are unchanged |
| Targets must be keys in the reference table (B8) | Relax V4 to any code 0 to 255, drop the target foreign key, and return a null name for unlisted codes |
| Mappings are global per keyboard model | Add a profile or device table and key `key_mappings` by it |
| Keys missing from the table, such as punctuation (0x2D to 0x38), are not remappable (B7) | Add rows to the catalogue; no code change |
| "Control" as a remap target means Left Ctrl (0xE0) | Documentation examples only |

Two of these change behaviour a user would notice, so they would be confirmed with the product owner before building further:

- Should assign update only the keys sent, or replace the whole set?
- Can a key be remapped to a code that is not a physical key on this keyboard, such as F13?

## Decision log

There are 27 decisions in four groups. Each row gives the choice, the alternative rejected and the reason. Decision IDs are stable and are not renumbered.

### Behaviour

| # | Decision | Choice | Rejected | Why |
| --- | --- | --- | --- | --- |
| B1 | What assign means | Merge: only the keys in the request change; every other key keeps its current mapping | Replace, where each request is the complete set; replace plus separate per-key routes | Remapping tools with a visual editor, such as VIA and SteelSeries GG, change one key at a time, so a user expects saving one key to leave the others alone. Merge also covers whole-set replace: sending all 92 keys from `GET` back sets every key. It stays idempotent. |
| B2 | Chained mappings (A→B, B→C) | Not resolved. Pressing A emits B | Transitive resolution | A mapping is physical key to emitted code. Resolving chains would make a swap (A→Z, Z→A) impossible to express. |
| B3 | Swaps and cycles | Allowed | Cycle detection | Follows from B2. A swap is the most common remap after a single reassignment. |
| B4 | Two keys mapped to one target | Allowed | Require targets to be unique | The common Caps Lock to Control remap leaves two keys emitting Control. |
| B5 | A code that no key emits any more | Allowed, no warning | Reject or warn | Remapping A→Z without Z→A is a legitimate user choice. A warning would need a success response body, and success returns none (A5). |
| B6 | Self-map (A→A) | Accepted and stored like any other mapping, which resets that key | Reject as invalid and add a per-key reset route; accept but delete the stored row instead of writing one | A key mapped to itself is what default means. Under merge it is the way to reset one key, and it lets a client send the full `GET` result back unchanged. Storing it like any other entry needs no special case in the service, repository or schema. |
| B7 | Which codes are valid sources | Only the 92 keys in `HidKeyCodes.md` | Any 8-bit code | Those are the keys on the keyboard. A key that is not on the keyboard cannot be pressed. |
| B8 | Which codes are valid targets | Only the same 92 keys | Any code 0x00 to 0xFF | The catalogue is the only set of codes the service can name and verify. This is an assumption; see above. |
| B9 | Validation failure handling | Reject the whole request and report every error | Save the valid entries; or stop at the first error | Partial saves leave state the client did not ask for. Reporting all errors saves the client a round trip per mistake. |

### API

| # | Decision | Choice | Rejected | Why |
| --- | --- | --- | --- | --- |
| A1 | Resource shape | One resource, `/api/keyboards/{keyboardName}/mappings`, with `GET`, `PATCH` (merge) and `DELETE` (reset all) | Separate `/assign` and `/list` RPC routes; `PUT` for replace; per-key sub-resources | All three operations act on the same thing. `PATCH` carries partial-update semantics by definition, and `DELETE` on the collection is the reset that merge otherwise lacks. |
| A2 | Keyboard identifier | The keyboard name as a URL-encoded path segment (`Apex%20Pro%20Gen%203`), matched ignoring case | A separate slug such as `apex-pro-gen-3`; the name in the query string or body | The directions give the keyboard name as the input. ASP.NET Core decodes the segment before routing, and matching ignoring case avoids a `404` for `apex pro gen 3`. |
| A3 | How keys are identified in requests | HID code as a JSON integer, 0 to 255 | Hex strings (`"0x04"`) or key names | JSON has no hex literal, so integers need no parsing rules. Names are ambiguous: Control could be 0xE0 or 0xE4. |
| A4 | What `GET` returns per key | Code, hex string and name for both source and target, plus `remapped` | Codes only | The response can be read without the reference table open. |
| A5 | Success response for `PATCH` and `DELETE` | `204 No Content` | `200` with the new state | Assign has no output beyond success or failure. |
| A6 | Error format and status | RFC 9457 problem details. `400` for validation, `404` for unknown keyboard | Custom error envelope; `422` for semantic errors | Problem details are built into ASP.NET Core. One status for all bad input is simpler for a client than splitting 400 and 422. |
| A7 | `PATCH` body format | `application/json` with a `mappings` list of `source` and `target` pairs | JSON Merge Patch (RFC 7396); JSON Patch (RFC 6902) | A list of remaps is the input the directions describe. Neither patch format addresses keys by integer code without extra conventions. |

### Persistence

| # | Decision | Choice | Rejected | Why |
| --- | --- | --- | --- | --- |
| P1 | Data access library | `Microsoft.Data.Sqlite` with hand-written SQL | EF Core; Dapper | The design needs three tables and a handful of statements. Raw SQL keeps the transaction visible and avoids migration tooling that would need installing to run the project. |
| P2 | Where the key catalogue lives | Seeded into SQLite from a catalogue class in code | In code only | Foreign keys can then stop an invalid code being stored, `GET` is one join, and a second keyboard is data rather than code. |
| P3 | What is stored per mapping | One row per key a client has set, at most one per key; a missing row means default | A row for every key, seeded at startup | The default is derivable, so seeding it creates 92 rows that can disagree with the catalogue. A save overwrites the key's row, so the table never holds more than 92 rows per keyboard. Reset-all becomes a delete. `remapped` is derived by comparing target with source, so a stored self-map reads the same as no row. |
| P4 | Schema creation | `CREATE TABLE IF NOT EXISTS` and `INSERT OR IGNORE` at startup | EF migrations; a migration library | The schema has one version. Idempotent startup SQL gives a working database on first run with no extra command. |
| P5 | Database file location | `ConnectionStrings:KeyMappings` in `appsettings.json`, default `keymappings.db` in the content root | Hard-coded path; in-memory | The file persists across runs as required, and tests can point at a temporary file. |
| P6 | Integrity enforcement | Validate in the service and again by constraint in the schema | Service validation only | The service gives readable errors. The constraints hold even if a later code path skips the validator. |

### Code structure and tooling

| # | Decision | Choice | Rejected | Why |
| --- | --- | --- | --- | --- |
| C1 | Runtime | .NET 10 | .NET 8 | .NET 10 is the current long-term support release, supported to November 2028. .NET 8 support ended on 10 November 2026. |
| C2 | Endpoint style | Controller with `[ApiController]` | Minimal API | The requirements ask for an object-oriented solution. A thin controller over a service and repository shows that structure in the conventional way. |
| C3 | Solution layout | One API project with folders, plus one test project | Separate Domain and Infrastructure assemblies | Three endpoints do not justify three assemblies. Interfaces keep the layers separable later. |
| C4 | Test and helper packages | xUnit and `Microsoft.AspNetCore.Mvc.Testing`, plus the two packages `dotnet test` needs to run xUnit | FluentAssertions, MediatR, AutoMapper | Recent versions of those three moved to commercial licences, and the project uses only freely licensed packages. Built-in asserts are enough here. |
| C5 | API documentation | Built-in OpenAPI document, a `.http` file and curl examples in the README | Swagger UI package | Three endpoints are documented fully by examples, with no extra dependency. |

## API contract

All three endpoints share one path. `keyboardName` is the keyboard's name, URL-encoded: `Apex%20Pro%20Gen%203`. It is matched ignoring case (A2).

### Assign mappings

`PATCH /api/keyboards/{keyboardName}/mappings`

```json
{
  "mappings": [
    { "source": 4,  "target": 29 },
    { "source": 33, "target": 31 }
  ]
}
```

In this example A (0x04) emits Z (0x1D), and 4 (0x21) emits 2 (0x1F). Every key not listed keeps its current mapping (B1). An entry whose target equals its source, such as `{ "source": 4, "target": 4 }`, returns that key to its default (B6). An empty `mappings` array changes nothing.

| Status | When | Body |
| --- | --- | --- |
| `204` | All entries valid and applied | None |
| `400` | Malformed JSON, missing `mappings`, or any validation rule fails | Problem details with an `errors` array |
| `404` | No keyboard with that name | Problem details |

Example `400` body:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more mappings are invalid.",
  "status": 400,
  "errors": [
    { "index": 1, "field": "source", "code": "unknown_key",
      "message": "45 (0x2D) is not a key on Apex Pro Gen 3." },
    { "index": 3, "field": "source", "code": "duplicate_source",
      "message": "4 (0x04) is already mapped at index 0." }
  ]
}
```

### Reset all mappings

`DELETE /api/keyboards/{keyboardName}/mappings`

Removes every stored mapping, so each key emits its own code again. The status is `204`, or `404` for an unknown name. It has no request body.

### Get all mappings

`GET /api/keyboards/{keyboardName}/mappings`

```json
{
  "keyboard": { "name": "Apex Pro Gen 3" },
  "mappings": [
    { "source": { "code": 4, "hex": "0x04", "name": "A" },
      "target": { "code": 29, "hex": "0x1D", "name": "Z" },
      "remapped": true },
    { "source": { "code": 5, "hex": "0x05", "name": "B" },
      "target": { "code": 5, "hex": "0x05", "name": "B" },
      "remapped": false }
  ]
}
```

The array always holds 92 entries, ordered by source code. The status is `200`, or `404` for an unknown name. The `keyboard.name` field gives the stored spelling, whatever case the request used.

## Validation rules

A `PATCH` is applied only if every rule passes. All failures are collected and returned together (B9).

| # | Rule | Error code | Why |
| --- | --- | --- | --- |
| V1 | Body is valid JSON with a `mappings` array, and every entry has `source` and `target` as JSON integers that fit a 32-bit integer. The array may be empty | `invalid_body` | An empty array asks for no change, which is not a mistake. A missing field, a string such as `"4"`, a fraction or `null` fails JSON binding before V2 can run, so it is reported here as one error with `index` null and `field` `body`. |
| V2 | Each entry has integer `source` and `target` between 0 and 255 | `out_of_range` | HID codes are 8-bit. The request model uses `int` so that 256 gets this error, not a generic binding failure. |
| V3 | `source` is a key on this keyboard | `unknown_key` | B7. |
| V4 | `target` is a key on this keyboard | `unknown_key` | B8. |
| V5 | No `source` appears twice in one request | `duplicate_source` | One key cannot emit two codes. Identical duplicate entries are also rejected, since they point to a client bug. |

Three things are deliberately not errors: several sources sharing a target (B4), swaps and cycles (B3), and self-maps (B6). Self-maps pass validation and are stored like any other entry.

Rules V3 and V4 are skipped for an entry that already failed V2, so each bad value produces one error.

## Data model

Three tables hold the keyboards, their key catalogue and the stored mappings.

```sql
CREATE TABLE IF NOT EXISTS keyboards (
    id    INTEGER PRIMARY KEY,
    name  TEXT NOT NULL UNIQUE COLLATE NOCASE
);

CREATE TABLE IF NOT EXISTS keyboard_keys (
    keyboard_id  INTEGER NOT NULL REFERENCES keyboards(id),
    hid_code     INTEGER NOT NULL CHECK (hid_code BETWEEN 0 AND 255),
    name         TEXT NOT NULL,
    PRIMARY KEY (keyboard_id, hid_code)
) WITHOUT ROWID;

CREATE TABLE IF NOT EXISTS key_mappings (
    keyboard_id  INTEGER NOT NULL,
    source_code  INTEGER NOT NULL,
    target_code  INTEGER NOT NULL,
    PRIMARY KEY (keyboard_id, source_code),
    FOREIGN KEY (keyboard_id, source_code)
        REFERENCES keyboard_keys (keyboard_id, hid_code),
    FOREIGN KEY (keyboard_id, target_code)
        REFERENCES keyboard_keys (keyboard_id, hid_code)
) WITHOUT ROWID;
```

The schema enforces the same rules as the validator (P6):

- The primary key on `key_mappings` allows one mapping per source key, matching V5.
- The two composite foreign keys require source and target to be keys on that keyboard, matching V3 and V4.
- `COLLATE NOCASE` on `keyboards.name` makes the lookup `WHERE name = @name` ignore case, matching A2.

SQLite ignores foreign keys unless they are switched on per connection, so the connection string sets `Foreign Keys=True`.

### Startup

`DatabaseInitializer` runs once before the app accepts requests. It creates the tables, then inserts the Apex Pro Gen 3 row and its 92 keys with `INSERT OR IGNORE`. Running it again changes nothing, so existing mappings survive a restart.

### Read

`GET` is one query. The left join supplies the stored mapping where one exists, and `COALESCE` falls back to the key itself (P3). A key counts as remapped when its target differs from its own code, so a stored self-map reads as not remapped (B6).

```sql
SELECT k.hid_code, k.name,
       t.hid_code AS target_code, t.name AS target_name,
       t.hid_code <> k.hid_code AS remapped
FROM keyboard_keys k
LEFT JOIN key_mappings m
       ON m.keyboard_id = k.keyboard_id AND m.source_code = k.hid_code
JOIN keyboard_keys t
       ON t.keyboard_id = k.keyboard_id
      AND t.hid_code = COALESCE(m.target_code, k.hid_code)
WHERE k.keyboard_id = @keyboardId
ORDER BY k.hid_code;
```

### Write

`PATCH` runs in one transaction, after validation has passed. Every entry, self-maps included, is the same upsert:

```sql
INSERT INTO key_mappings (keyboard_id, source_code, target_code)
VALUES (@keyboardId, @source, @target)
ON CONFLICT (keyboard_id, source_code)
DO UPDATE SET target_code = excluded.target_code;
```

Then commit. Any exception rolls back and leaves the previous set in place. Because the primary key allows one row per key, a save overwrites rather than adds, and the table holds at most 92 rows per keyboard.

`DELETE` is one statement: `DELETE FROM key_mappings WHERE keyboard_id = @keyboardId`.

SQLite allows one writer at a time, so two concurrent writes run one after the other. Where both change the same key, the later one wins; keys only one of them touched keep that change.

## Architecture

The code has three layers in one project: a controller for HTTP, a service for rules, and a repository for SQL (C2, C3).

```text
KeyRemap.sln
DESIGN.md
HidKeyCodes.md
README.md
requests.http
src/KeyRemap.Api/
  Program.cs
  appsettings.json
  Controllers/  KeyboardMappingsController, MappingProblems
  Contracts/    AssignMappingsRequest, MappingEntry, KeyboardMappingsResponse
  Domain/       Keyboard, HidKey, KeyMapping, MappingValidator, ValidationError
  Services/     IKeyMappingService, KeyMappingService
  Data/         IKeyboardRepository, SqliteKeyboardRepository,
                DatabaseInitializer, ApexProGen3Catalog
tests/KeyRemap.Tests/
```

| Class | Responsibility | Knows about |
| --- | --- | --- |
| `KeyboardMappingsController` | Bind the request, call the service, turn its result into `200`, `204`, `400` or `404` | HTTP and the service interface |
| `MappingProblems` | Build the `400` problem details body, for validation errors and for a body that fails binding | HTTP and `ValidationError` |
| `KeyMappingService` | Load the keyboard, run the validator, call the repository | Domain types and the repository interface |
| `MappingValidator` | Apply V2 to V5 to a request against a keyboard's key set and return all errors | Domain types only; no I/O |
| `SqliteKeyboardRepository` | Find a keyboard by name, read all mappings, upsert mappings in a transaction, clear all mappings | SQL and `Microsoft.Data.Sqlite` |
| `DatabaseInitializer` | Create the schema and seed the catalogue at startup | SQL and the catalogue |
| `ApexProGen3Catalog` | The 92 keys from `HidKeyCodes.md` as a static list | Nothing |

A `PATCH` request passes through the layers in this order:

1. The controller binds the JSON body to `AssignMappingsRequest`. A body that fails binding returns `400` with one `invalid_body` error, in the same shape as validation errors (V1).
2. The service asks the repository for the keyboard, by name, and its keys. No keyboard returns `404`.
3. `MappingValidator` checks every entry. Any error returns `400` with the full list.
4. The service passes the entries to the repository, which upserts them in one transaction.
5. The controller returns `204`.

A `DELETE` request skips steps 1 and 3: the service finds the keyboard and the repository clears its mappings.

Three smaller choices follow from this structure:

- **The validator is a separate class with no I/O.** It holds most of the logic, so it should be testable without a database or web host.
- **The service returns a result object, not exceptions.** Not-found and invalid are expected outcomes, and the controller maps each to a status code in one place.
- **The repository opens a connection per operation.** `Microsoft.Data.Sqlite` pools connections, and this avoids sharing one connection across requests.

## Testing

Tests are split by layer: unit tests on the validator, and integration tests through the real HTTP pipeline against a temporary SQLite file.

| Kind | Test | Proves |
| --- | --- | --- |
| Unit | Catalogue has 92 keys with unique codes | B7 |
| Unit | Valid set with a swap and a shared target passes | B3, B4 |
| Unit | Self-map passes validation | B6 |
| Unit | Source or target not in the catalogue fails with `unknown_key` | V3, V4 |
| Unit | Value of -1 or 256 fails with `out_of_range` and no second error | V2 |
| Unit | Repeated source fails with `duplicate_source` and names both indexes | V5 |
| Unit | Request with three different mistakes returns three errors | B9 |
| Integration | `GET` on a fresh database returns 92 keys, none remapped | Seeding, P3 |
| Integration | `PATCH` a two-key remap, then `GET` shows A→Z and 4→2 | Round trip |
| Integration | A second `PATCH` with a different key keeps the first two | B1 |
| Integration | A `PATCH` that changes an already-remapped key's target updates it | B1, upsert |
| Integration | A self-map on a remapped key resets it, reads as not remapped, and leaves other keys remapped | B6, P3 |
| Integration | Remapping the same key several times leaves one stored row for it | P3, upsert |
| Integration | Sending the `GET` result back as a `PATCH` returns `204` and changes nothing | B1, B6 |
| Integration | `PATCH` with an empty array returns `204` and changes nothing | V1 |
| Integration | `DELETE` resets every key | A1, P3 |
| Integration | An invalid `PATCH` returns `400` and leaves earlier mappings unchanged | B9, transaction |
| Integration | The keyboard name is matched ignoring case | A2 |
| Integration | Unknown keyboard name returns `404` on all three verbs | A6 |
| Integration | Mappings survive disposing the host and starting a new one on the same file | Persistence, idempotent seed |

Integration tests use `WebApplicationFactory` with the connection string overridden to a unique temporary file per test, deleted on dispose.

## Running

The only prerequisite is the .NET 10 SDK; the database file is created on first run.

```bash
dotnet run --project src/KeyRemap.Api      # listens on http://localhost:5080
dotnet test

curl -i -X PATCH "http://localhost:5080/api/keyboards/Apex%20Pro%20Gen%203/mappings" \
  -H "Content-Type: application/json" \
  -d '{"mappings":[{"source":4,"target":29},{"source":33,"target":31}]}'

curl "http://localhost:5080/api/keyboards/Apex%20Pro%20Gen%203/mappings"

curl -i -X DELETE "http://localhost:5080/api/keyboards/Apex%20Pro%20Gen%203/mappings"
```

The port is fixed in `launchSettings.json` so the examples work as written. `keymappings.db` is in `.gitignore`.

| Package | Used for | Licence |
| --- | --- | --- |
| `Microsoft.Data.Sqlite` | SQLite access | MIT |
| `Microsoft.AspNetCore.OpenApi` | OpenAPI document | MIT |
| `xunit` | Test framework | Apache-2.0 |
| `Microsoft.AspNetCore.Mvc.Testing` | In-process test host | MIT |
| `Microsoft.NET.Test.Sdk` | Runs tests under `dotnet test` | MIT |
| `xunit.runner.visualstudio` | xUnit adapter for `dotnet test` | Apache-2.0 |

## Next steps (out of scope)

None of these is required, so none is built. They are listed in the order they would be taken up, with the cost of each given the current design.

| # | Next step | Why it is not in this version | What it would take |
| --- | --- | --- | --- |
| N1 | Per-key routes and whole-set replace | `PATCH` already covers both: one entry changes one key, and the full `GET` result replaces every key (B1, B6) | `PUT` and `DELETE` on `/mappings/{source}` for single keys, and `PUT` on `/mappings` for replace. The validator and schema are reused as they are. |
| N2 | Optimistic concurrency | The requirements describe a single caller | Return an `ETag` from `GET` and require `If-Match` on `PATCH` and `DELETE`, backed by a version column on `keyboards`. |
| N3 | Warn when a key code can no longer be typed | Assign returns success or failure only (B5) | A pure function over the mapping set, returned as a warnings array or a dry-run validate endpoint. |
| N4 | More keyboard models | One keyboard is required | A catalogue class and seed rows per model. No change to endpoints or queries (P2). |
| N5 | Profiles, users or per-device mappings | Mappings are keyed by keyboard name only | A profile table, and `key_mappings` keyed by profile. The route gains a profile segment. |
| N6 | Authentication and authorisation | The service runs locally | ASP.NET Core authentication middleware and an ownership check, which depends on N5. |
| N7 | Versioned schema migrations | The schema has one version (P4) | A version table and ordered scripts run by `DatabaseInitializer`. |
| N8 | Apply mappings to the physical device | Only storing and returning mappings is required | A separate component that reads the stored set and writes it to the keyboard over HID. |
| N9 | Deployment packaging | Only a local run is required | A Dockerfile with the database on a mounted volume, a health endpoint, and a CI build that runs the tests. |

## Change log

Each entry records a change to a decision or to the design after the initial version: the date, what changed (with the decision ID where one applies) and why.

| Date | Change | Reason |
| --- | --- | --- |
| 2026-11-19 | Initial design | |
| 2026-11-19 | B1: assign merges into the current set instead of replacing it. B6: a self-map resets that key. A1, A5: `PATCH` and `DELETE` replace `PUT`. A7 added for the `PATCH` body. V1, the write path, tests and N1 updated to match | Remapping tools change one key at a time, so replacing the whole set on each save would surprise users. Merge still allows a full replace by sending all 92 keys. |
| 2026-11-19 | A2: the keyboard is addressed by its name, matched ignoring case. The `slug` column is dropped and `keyboards.name` uses `COLLATE NOCASE` | The directions give the keyboard name as the input. |
| 2026-11-19 | B6, P3: a self-map is stored like any other mapping. `remapped` is derived by comparing target with source. The `CHECK (source_code <> target_code)` constraint and the service's split into resets and upserts are removed | One write path with no special case. A save overwrites the key's row, so storage stays bounded at 92 rows per keyboard. |
| 2026-11-19 | C4: test-runner packages `Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio` listed | `dotnet test` cannot run xUnit tests without them. |
| 2026-11-19 | V1: a missing or non-integer `source` or `target` is `invalid_body`. Numbers sent as strings are rejected (A3). The framework's default binding error is replaced, and `MappingProblems` added to build both `400` bodies | Those inputs fail binding before V2 can run. ASP.NET Core accepts `"4"` as 4 by default, which A3 rules out, and every `400` should have the same shape. |
| 2026-11-19 | Testing: temporary database per test rather than per test class | Tests then cannot see each other's mappings. |

## References

- [.NET and .NET Core support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core), for the support dates in C1.
- [`HidKeyCodes.md`](HidKeyCodes.md), for the 92-key catalogue.
