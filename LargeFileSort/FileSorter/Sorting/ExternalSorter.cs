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
        EnsureDiskSpace(options.OutputPath, fileLength);

        if (fileLength == 0)
        {
            File.WriteAllBytes(options.OutputPath, []);
            return new SortResult { RunCount = 0, MergePassCount = 0 };
        }

        if (fileLength <= options.ChunkSize)
        {
            SortInMemory(options);
            return new SortResult { RunCount = 0, MergePassCount = 0, UsedFastPath = true };
        }

        bool useDefaultTemp = string.IsNullOrWhiteSpace(options.TempDirectory);
        string tempDirectory = useDefaultTemp
            ? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(options.OutputPath)) ?? ".", ".sort-tmp")
            : options.TempDirectory!;
        bool createdDefaultTemp = useDefaultTemp && !Directory.Exists(tempDirectory);
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

    private static void SortInMemory(SortOptions options)
    {
        using FileStream input = new(
            options.InputPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            options.ChunkSize,
            FileOptions.SequentialScan);
        using var reader = new ChunkReader(input, options.ChunkSize, options.MaxLineLength, leaveOpen: true);

        var lines = new List<byte[]>();
        while (reader.MoveNextChunk())
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            foreach (LineRef line in reader.Lines)
            {
                lines.Add(reader.Buffer.AsSpan(line.Start, line.End - line.Start).ToArray());
            }
        }

        lines.Sort(static (left, right) => LineComparer.CompareLines(left, right));

        using FileStream output = File.Create(options.OutputPath);
        foreach (byte[] line in lines)
        {
            output.Write(line);
            output.WriteByte((byte)'\n');
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
        using var work = new BlockingCollection<ChunkWork>();
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

        try
        {
            using FileStream input = OpenInput(options);
            using var reader = new ChunkReader(input, options.ChunkSize, options.MaxLineLength, leaveOpen: true);

            while (reader.MoveNextChunk())
            {
                options.CancellationToken.ThrowIfCancellationRequested();
                work.Add(ChunkWork.Copy(reader.Buffer, reader.Lines), options.CancellationToken);
            }
        }
        finally
        {
            work.CompleteAdding();
            Task.WaitAll(workers);
        }
    }

    private static int MergeBufferSize(SortOptions options) =>
        Math.Clamp(options.ChunkSize, 64 * 1024, 1024 * 1024);

    private static FileStream OpenInput(SortOptions options) =>
        new(
            options.InputPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            options.ChunkSize,
            FileOptions.SequentialScan);

    private static SortOptions ApplyMemoryBudget(SortOptions options)
    {
        if (options.MaxMemoryBytes is not long memory)
        {
            return options;
        }

        int chunkSize = MemoryBudget.ResolveChunkSize(memory, options.DegreeOfParallelism);
        Console.WriteLine(
            $"Memory budget: {memory} bytes; chunk size: {chunkSize}; workers: {options.DegreeOfParallelism}.");

        return new SortOptions
        {
            InputPath = options.InputPath,
            OutputPath = options.OutputPath,
            TempDirectory = options.TempDirectory,
            ChunkSize = chunkSize,
            MaxLineLength = options.MaxLineLength,
            MaxFanIn = options.MaxFanIn,
            DegreeOfParallelism = options.DegreeOfParallelism,
            MaxMemoryBytes = options.MaxMemoryBytes,
            KeepTemp = options.KeepTemp,
            CancellationToken = options.CancellationToken,
        };
    }

    private static void EnsureDiskSpace(string outputPath, long inputLength)
    {
        string? root = Path.GetPathRoot(Path.GetFullPath(outputPath));
        if (string.IsNullOrEmpty(root))
        {
            return;
        }

        long needed = inputLength * 2;
        var drive = new DriveInfo(root);
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
