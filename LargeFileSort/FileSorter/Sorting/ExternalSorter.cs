using System.Collections.Concurrent;
using FileSorter.Sorting.Merging;
using FileSorter.Sorting.Parsing;
using FileSorter.Sorting.Runs;

namespace FileSorter.Sorting;

public sealed class ExternalSorter
{
    private readonly RunBuilder _runBuilder = new();
    private readonly KWayMerger _merger = new();

    public SortResult Sort(SortOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.InputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OutputPath);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.ChunkSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxLineLength, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxFanIn, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.DegreeOfParallelism, 1);

        options = ApplyMemoryBudget(options);
        options.CancellationToken.ThrowIfCancellationRequested();

        if (string.Equals(
                Path.GetFullPath(options.InputPath),
                Path.GetFullPath(options.OutputPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Input and output paths must be different.");
        }

        if (!File.Exists(options.InputPath))
        {
            throw new FileNotFoundException("Input file was not found.", options.InputPath);
        }

        long fileLength = new FileInfo(options.InputPath).Length;

        if (fileLength == 0)
        {
            File.WriteAllBytes(options.OutputPath, []);
            return new SortResult { RunCount = 0, MergePassCount = 0 };
        }

        if (fileLength <= options.ChunkSize)
        {
            EnsureDiskSpace(options.OutputPath, fileLength);
            SortInMemory(options);
            return new SortResult { RunCount = 0, MergePassCount = 0, UsedFastPath = true };
        }

        bool useDefaultTemp = string.IsNullOrWhiteSpace(options.TempDirectory);
        string tempDirectory = useDefaultTemp
            ? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(options.OutputPath)) ?? ".", ".sort-tmp")
            : options.TempDirectory!;
        bool createdDefaultTemp = useDefaultTemp && !Directory.Exists(tempDirectory);
        EnsureDiskSpace(options.OutputPath, tempDirectory, fileLength);
        Directory.CreateDirectory(tempDirectory);

        var allTempFiles = new List<string>();
        var currentRuns = new List<string>();

        try
        {
            BuildRuns(options, tempDirectory, allTempFiles, currentRuns);

            if (currentRuns.Count == 0)
            {
                File.WriteAllBytes(options.OutputPath, []);
                return new SortResult { RunCount = 0, MergePassCount = 0 };
            }

            int runCount = currentRuns.Count;
            int mergePasses = 0;
            int generation = 0;

            while (currentRuns.Count > options.MaxFanIn)
            {
                currentRuns = MergeGeneration(
                    currentRuns,
                    tempDirectory,
                    generation,
                    options,
                    allTempFiles);
                generation++;
                mergePasses++;
            }

            _merger.Merge(
                currentRuns,
                options.OutputPath,
                MergeBufferSize(options),
                options.MaxLineLength,
                options.CancellationToken);
            mergePasses++;

            return new SortResult { RunCount = runCount, MergePassCount = mergePasses };
        }
        finally
        {
            if (!options.KeepTemp)
            {
                foreach (string path in allTempFiles)
                {
                    TryDelete(path);
                }

                if (createdDefaultTemp)
                {
                    TryDeleteDirectoryIfEmpty(tempDirectory);
                }
            }
        }
    }

    private void SortInMemory(SortOptions options)
    {
        using FileStream input = OpenInput(options);
        using var reader = new ChunkReader(input, options.ChunkSize, options.MaxLineLength, leaveOpen: true);

        if (!reader.MoveNextChunk())
        {
            File.WriteAllBytes(options.OutputPath, []);
            return;
        }

        options.CancellationToken.ThrowIfCancellationRequested();
        WriteOutputAtomically(options.OutputPath, reader.Buffer, reader.Lines);

        if (reader.MoveNextChunk())
        {
            TryDelete(options.OutputPath);
            throw new InvalidOperationException("Fast path expected a single chunk.");
        }
    }

    private void WriteOutputAtomically(string outputPath, byte[] buffer, ReadOnlySpan<LineRef> lines)
    {
        string partialPath = outputPath + ".partial";
        try
        {
            _runBuilder.Write(partialPath, buffer, lines);
            File.Move(partialPath, outputPath, overwrite: true);
        }
        catch
        {
            TryDelete(partialPath);
            throw;
        }
    }

    private void BuildRuns(
        SortOptions options,
        string tempDirectory,
        List<string> allTempFiles,
        List<string> currentRuns)
    {
        if (options.DegreeOfParallelism == 1)
        {
            BuildRunsSequential(options, tempDirectory, allTempFiles, currentRuns);
            return;
        }

        BuildRunsParallel(options, tempDirectory, allTempFiles, currentRuns);
    }

    private void BuildRunsSequential(
        SortOptions options,
        string tempDirectory,
        List<string> allTempFiles,
        List<string> currentRuns)
    {
        using FileStream input = OpenInput(options);
        using var reader = new ChunkReader(input, options.ChunkSize, options.MaxLineLength, leaveOpen: true);

        int runIndex = 0;
        while (reader.MoveNextChunk())
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            string runPath = Path.Combine(tempDirectory, $"run-{runIndex:D6}.run");
            allTempFiles.Add(runPath);
            currentRuns.Add(runPath);
            _runBuilder.Write(runPath, reader.Buffer, reader.Lines);
            runIndex++;
        }
    }

    private void BuildRunsParallel(
        SortOptions options,
        string tempDirectory,
        List<string> allTempFiles,
        List<string> currentRuns)
    {
        using var work = new BlockingCollection<ChunkWork>(boundedCapacity: MemoryBudget.DefaultQueueDepth);
        int runIndex = 0;
        var workers = new Task[options.DegreeOfParallelism];

        for (int i = 0; i < workers.Length; i++)
        {
            workers[i] = Task.Run(() =>
            {
                foreach (ChunkWork chunk in work.GetConsumingEnumerable())
                {
                    options.CancellationToken.ThrowIfCancellationRequested();
                    int index = Interlocked.Increment(ref runIndex) - 1;
                    string runPath = Path.Combine(tempDirectory, $"run-{index:D6}.run");
                    lock (allTempFiles)
                    {
                        allTempFiles.Add(runPath);
                        currentRuns.Add(runPath);
                    }

                    _runBuilder.Write(runPath, chunk.Buffer, chunk.Lines);
                }
            }, options.CancellationToken);
        }

        Exception? pending = null;
        try
        {
            using FileStream input = OpenInput(options);
            using var reader = new ChunkReader(input, options.ChunkSize, options.MaxLineLength, leaveOpen: true);

            while (reader.MoveNextChunk())
            {
                options.CancellationToken.ThrowIfCancellationRequested();
                ChunkWork chunk = ChunkWork.Copy(reader.Buffer, reader.Lines);
                while (!work.TryAdd(chunk, 50, options.CancellationToken))
                {
                    if (workers.Any(static worker => worker.IsFaulted || worker.IsCanceled))
                    {
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            pending = ex;
        }
        finally
        {
            work.CompleteAdding();
            try
            {
                Task.WaitAll(workers);
            }
            catch (AggregateException aggregate)
            {
                pending ??= Unwrap(aggregate);
            }
        }

        if (pending is not null)
        {
            throw pending;
        }
    }

    private static Exception Unwrap(AggregateException aggregate)
    {
        AggregateException flat = aggregate.Flatten();
        return flat.InnerExceptions[0];
    }

    private static int MergeBufferSize(SortOptions options) =>
        Math.Clamp(options.ChunkSize, 64 * 1024, 1024 * 1024);

    // ChunkReader already owns the read buffer; FileStream buffering would add a second full-size copy.
    private static FileStream OpenInput(SortOptions options) =>
        new(
            options.InputPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1,
            FileOptions.SequentialScan);

    private static SortOptions ApplyMemoryBudget(SortOptions options)
    {
        if (options.MaxMemoryBytes is not long memory)
        {
            return options;
        }

        int chunkSize = MemoryBudget.ResolveChunkSize(
            memory,
            options.DegreeOfParallelism,
            MemoryBudget.DefaultQueueDepth);
        Console.WriteLine(
            $"Memory budget: {memory} bytes; chunk size: {chunkSize}; workers: {options.DegreeOfParallelism}.");

        return options with { ChunkSize = chunkSize };
    }

    private static void EnsureDiskSpace(string outputPath, long inputLength) =>
        CheckDrive(outputPath, inputLength);

    private static void EnsureDiskSpace(string outputPath, string tempDirectory, long inputLength)
    {
        if (SameVolume(outputPath, tempDirectory))
        {
            CheckDrive(outputPath, inputLength * 2);
            return;
        }

        CheckDrive(outputPath, inputLength);
        CheckDrive(tempDirectory, inputLength);
    }

    private static bool SameVolume(string first, string second)
    {
        string? firstRoot = TryGetPathRoot(first);
        string? secondRoot = TryGetPathRoot(second);
        return firstRoot is not null
            && secondRoot is not null
            && string.Equals(firstRoot, secondRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryGetPathRoot(string path)
    {
        try
        {
            return Path.GetPathRoot(Path.GetFullPath(path));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static void CheckDrive(string path, long needed)
    {
        string? root = TryGetPathRoot(path);
        if (string.IsNullOrEmpty(root))
        {
            return;
        }

        DriveInfo drive;
        try
        {
            drive = new DriveInfo(root);
        }
        catch (ArgumentException)
        {
            return;
        }

        if (!drive.IsReady)
        {
            return;
        }

        if (drive.AvailableFreeSpace < needed)
        {
            throw new IOException(
                $"Not enough free space on '{root}'. Need about {needed} bytes for temporary runs and output.");
        }
    }

    private readonly record struct ChunkWork(byte[] Buffer, LineRef[] Lines)
    {
        public static ChunkWork Copy(byte[] buffer, ReadOnlySpan<LineRef> lines)
        {
            if (lines.Length == 0)
            {
                return new ChunkWork([], []);
            }

            int start = lines[0].Start;
            int end = lines[0].End;
            foreach (LineRef line in lines)
            {
                start = Math.Min(start, line.Start);
                end = Math.Max(end, line.End);
            }

            byte[] copy = new byte[end - start];
            System.Buffer.BlockCopy(buffer, start, copy, 0, copy.Length);

            var adjusted = new LineRef[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                LineRef line = lines[i];
                adjusted[i] = new LineRef(line.Start - start, line.TextStart - start, line.End - start);
            }

            return new ChunkWork(copy, adjusted);
        }
    }

    private List<string> MergeGeneration(
        List<string> currentRuns,
        string tempDirectory,
        int generation,
        SortOptions options,
        List<string> allTempFiles)
    {
        var next = new List<string>();
        int groupIndex = 0;

        for (int start = 0; start < currentRuns.Count; start += options.MaxFanIn)
        {
            int count = Math.Min(options.MaxFanIn, currentRuns.Count - start);
            List<string> group = currentRuns.GetRange(start, count);

            if (group.Count == 1)
            {
                next.Add(group[0]);
                groupIndex++;
                continue;
            }

            string mergedPath = Path.Combine(tempDirectory, $"merge-{generation:D2}-{groupIndex:D6}.run");
            allTempFiles.Add(mergedPath);
            next.Add(mergedPath);
            _merger.Merge(
                group,
                mergedPath,
                MergeBufferSize(options),
                options.MaxLineLength,
                options.CancellationToken);
            foreach (string consumed in group)
            {
                TryDelete(consumed);
            }

            groupIndex++;
        }

        return next;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteDirectoryIfEmpty(string path)
    {
        try
        {
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
            {
                Directory.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
