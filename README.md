# LargeFileSort

Two .NET 10 console programs for the 3dEYE take-home: a test-file generator and an external merge sort for lines of the form `<Number>. <String>`.

Requires the **.NET 10 SDK** (the solution uses `.slnx`). Solution path: `LargeFileSort/LargeFileSort.slnx`.

## Line format

Each line is leading digits, then the first `.` immediately followed by one space, then the rest of the line (may be empty). A period inside the text stays in the text (`12.34. Apple` is invalid).

- Leading UTF-8 BOM on the file is skipped; a BOM inside a line is invalid.
- `\n` and `\r\n` are accepted. `\r` is not part of the sort key.
- A last line without a trailing newline is included.
- Output always uses `\n`.
- Invalid lines fail fast with a 1-based line number and a file-absolute byte offset.
- Numbers are compared as digit strings (leading zeros ignored for magnitude, then raw bytes). They are never parsed as `int`/`long`.
- Text is compared as UTF-8 ordinal bytes, not culture collation.

The generator emits `int` numbers without leading zeros. The sorter still accepts leading zeros, non-ASCII text, and numbers wider than `Int64`.

## Order

1. Text part, UTF-8 ordinal.
2. Number as an arbitrary-length digit string (leading zeros ignored for magnitude).
3. Tie-break on the original digit bytes (`007` before `7`).

## Generator

```powershell
dotnet run --project LargeFileSort/FileGenerator -- --output sample.txt --size 10MB --seed 42
```

| Flag | Default |
| --- | --- |
| `--output` | required |
| `--size` | required (`1024`, `10MB`, `1GB`, …) |
| `--seed` | random, printed before the write |
| `--unique-strings` | 1000 |
| `--max-number` | 1000000000 |
| `--force` | overwrite |

The file size is exact. Parallel generation was skipped: the assignment grades the sorter.

## Sorter

```powershell
dotnet run --project LargeFileSort/FileSorter -- --input sample.txt --output sorted.txt --chunk-size 64KB --max-fan-in 3 --verify
```

| Flag | Meaning |
| --- | --- |
| `--input` / `--output` | required, must be different paths |
| `--temp` | run directory (a caller-supplied folder is not deleted) |
| `--chunk-size` | phase-1 block (default 64 MB) |
| `--max-line-length` | reject a single line longer than this (default 1 MB); does not cap the chunk |
| `--max-memory` | override chunk size from a memory budget (min chunk 64 MB) |
| `--max-fan-in` | multi-pass merge when there are more runs (default 64, minimum 2) |
| `--degree-of-parallelism` | phase-1 workers (CLI default `min(CPU, 8)`; library API default is 1) |
| `--keep-temp` | leave run files |
| `--verify` | check output is sorted and has the same line count and line-hash sum as input |

Algorithm: read chunks, sort each to a run, k-way merge (several passes if needed). If the file fits in one chunk, sort in memory and skip temp files.

Phase-1 workers share a bounded queue (depth 1). Peak memory is about `(workers + 1) × 1.6 × chunk`.

Disk: about **1× input** on the output volume and **1× input** on the temp volume (about **2×** if they are the same volume). Intermediate runs are deleted between merge generations.

## Tests

```powershell
dotnet test LargeFileSort/LargeFileSort.slnx
```

## 100 GB

This machine cannot hold input + runs + output (~300 GB). Correctness is covered on small files, including multi-pass merge with a tiny `--chunk-size` and `--max-fan-in`. A 100 GB run is the same algorithm with a 64 MB chunk: about 1600 runs and two merge passes when `--max-fan-in` is 64.

## Decisions

- **Ordinal UTF-8**, not `StringComparer.CurrentCulture`: deterministic, no decode on the hot path.
- **External merge sort**, not distribution sort: the assignment guarantees many duplicate strings, which skew buckets.
- **No mmap / SIMD**: they do not replace a correct, testable baseline.
- **Phase-1 parallelism only**: merge stays serial so the heap stays simple.
- **No shared library** between the two programs: they are separate tools. `ByteSize` is duplicated on purpose.

## License

Take-home assignment code; not a product.
