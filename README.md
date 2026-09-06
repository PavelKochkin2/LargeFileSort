# LargeFileSort

Two .NET 10 console programs for the 3dEYE take-home: a test-file generator and an external merge sort for lines of the form `<Number>. <String>`.

Solution: `LargeFileSort/LargeFileSort.slnx`.

## Order

1. Text part, UTF-8 ordinal (not culture collation).
2. Number as an arbitrary-length digit string (leading zeros ignored for magnitude).
3. Tie-break on the original digit bytes (`007` before `7`).

Invalid lines fail fast with a line number and byte offset. Numbers are never parsed as `int`/`long`.

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
| `--temp` | run directory (caller folder is not deleted) |
| `--chunk-size` | phase-1 block (default 64 MB) |
| `--max-memory` | override chunk size from a memory budget (min chunk 64 MB) |
| `--max-fan-in` | multi-pass merge when there are more runs (default 64) |
| `--degree-of-parallelism` | phase-1 workers (default `min(CPU, 8)`) |
| `--keep-temp` | leave run files |
| `--verify` | check output is sorted and has the same line-hash sum as input |

Algorithm: read chunks, sort each to a run, k-way merge (several passes if needed). If the file fits in one chunk, sort in memory and skip temp files.

## Tests

```powershell
dotnet test LargeFileSort/LargeFileSort.slnx
```

## 100 GB

This machine cannot hold input + runs + output (~300 GB). Correctness is covered on small files, including multi-pass merge with a tiny `--chunk-size` and `--max-fan-in`. Throughput on 1–20 GB can be measured locally; a linear extrapolation to 100 GB is only an estimate.

## Decisions

- **Ordinal UTF-8**, not `StringComparer.CurrentCulture`: deterministic, no decode on the hot path.
- **External merge sort**, not distribution sort: the assignment guarantees many duplicate strings, which skew buckets.
- **No mmap / SIMD**: they do not replace a correct, testable baseline.
- **Phase-1 parallelism only**: merge stays serial so the heap stays simple.

## License

Take-home assignment code; not a product.
