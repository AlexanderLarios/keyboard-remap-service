# Keyboard Key Remapping Service

A small HTTP service that stores key remappings for the Apex Pro Gen 3 keyboard in SQLite. A client can remap any of the keyboard's 92 keys to another of its keys, reset one key or all of them, and read back every key with the HID usage code it currently emits. Mappings persist between runs.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Run

```bash
dotnet run --project src/KeyRemap.Api
```

The service listens on `http://localhost:5080`. The database file `keymappings.db` is created in `src/KeyRemap.Api` on first run.

## Test

```bash
dotnet test
```

## Examples

The keyboard is addressed by its name, URL-encoded. Keys are identified by their HID usage code as an integer; see [`HidKeyCodes.md`](HidKeyCodes.md).

Remap A (0x04) to Z (0x1D) and 4 (0x21) to 2 (0x1F). Other keys keep their current mapping. Returns `204 No Content`.

```bash
curl -i -X PATCH "http://localhost:5080/api/keyboards/Apex%20Pro%20Gen%203/mappings" \
  -H "Content-Type: application/json" \
  -d '{"mappings":[{"source":4,"target":29},{"source":33,"target":31}]}'
```

Read all 92 keys. Returns `200 OK`; the first two entries after the request above are:

```bash
curl "http://localhost:5080/api/keyboards/Apex%20Pro%20Gen%203/mappings"
```

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

Reset every key to its default. Returns `204 No Content`. To reset a single key, `PATCH` it to itself, for example `{"source":4,"target":4}`.

```bash
curl -i -X DELETE "http://localhost:5080/api/keyboards/Apex%20Pro%20Gen%203/mappings"
```

An invalid request returns `400` with every error listed, and nothing is saved. An unknown keyboard name returns `404`.

## Assumptions

| Assumption | If it is wrong, the change is |
| --- | --- |
| Assign merges into the current set (B1) | Add `PUT` for whole-set replace; storage and validator are unchanged |
| Targets must be keys in the reference table (B8) | Relax V4 to any code 0 to 255, drop the target foreign key, and return a null name for unlisted codes |
| Mappings are global per keyboard model | Add a profile or device table and key `key_mappings` by it |
| Keys missing from the table, such as punctuation (0x2D to 0x38), are not remappable (B7) | Add rows to the catalogue; no code change |
| "Control" as a remap target means Left Ctrl (0xE0) | Documentation examples only |

## Design

Decisions, the API contract, validation rules and the schema are in [`DESIGN.md`](DESIGN.md).
