using System.Text;
using FileSorter.Sorting;
using FileSorter.Sorting.Parsing;

namespace FileSorter.Tests;

public class ExternalSorterTests
{
    private readonly ExternalSorter _sorter = new();

    [Fact]
    public void Sort_EmptyFile_WritesEmptyOutput()
    {
        using var files = new TempSortFiles();
        File.WriteAllBytes(files.Input, []);

        _sorter.Sort(files.Options());

        Assert.Equal(0, new FileInfo(files.Output).Length);
    }

    [Fact]
    public void Sort_MatchesAssignmentExample()
    {
        using var files = new TempSortFiles();
        WriteInput(
            files.Input,
            """
            415. Apple
            30432. Something something something
            1. Apple
            32. Cherry is the best
            2. Banana is yellow
            """);

        _sorter.Sort(files.Options(chunkSize: 64));

        Assert.Equal(
            [
                "1. Apple",
                "415. Apple",
                "2. Banana is yellow",
                "32. Cherry is the best",
                "30432. Something something something",
            ],
            ReadLines(files.Output));
    }

    [Fact]
    public void Sort_TinyChunks_MatchesOracle()
    {
        using var files = new TempSortFiles();
        string[] lines =
        [
            "9. Zebra",
            "1. Apple",
            "007. Apple",
            "7. Apple",
            "2. Banana is yellow",
            "1. Яблоко",
        ];
        WriteInput(files.Input, string.Join('\n', lines));

        _sorter.Sort(files.Options(chunkSize: 8));

        Assert.Equal(Oracle(lines), ReadLines(files.Output));
    }

    [Fact]
    public void Sort_KeepsDuplicateLines()
    {
        using var files = new TempSortFiles();
        WriteInput(files.Input, "2. Apple\n1. Apple\n1. Apple\n");

        _sorter.Sort(files.Options(chunkSize: 16));

        Assert.Equal(["1. Apple", "1. Apple", "2. Apple"], ReadLines(files.Output));
    }

    [Fact]
    public void Sort_LastLineWithoutNewline_IsIncluded()
    {
        using var files = new TempSortFiles();
        File.WriteAllBytes(files.Input, "2. Banana\n1. Apple"u8.ToArray());

        _sorter.Sort(files.Options());

        Assert.Equal(["1. Apple", "2. Banana"], ReadLines(files.Output));
    }

    [Fact]
    public void Sort_FileLengthEqualsChunkSize_WithoutTrailingNewline()
    {
        using var files = new TempSortFiles();
        byte[] input = "2. Banana\n1. Apple"u8.ToArray();
        File.WriteAllBytes(files.Input, input);

        SortResult result = _sorter.Sort(files.Options(chunkSize: input.Length));

        Assert.True(result.UsedFastPath);
        Assert.Equal(["1. Apple", "2. Banana"], ReadLines(files.Output));
    }

    [Fact]
    public void Sort_WhenMaxLineLengthIsSmallerThanFile_StillReadsEveryLine()
    {
        using var files = new TempSortFiles();
        WriteInput(files.Input, "2. Banana is yellow\n1. Apple");

        SortResult result = _sorter.Sort(files.Options(chunkSize: 1024, maxLineLength: 24));

        Assert.True(result.UsedFastPath);
        Assert.Equal(["1. Apple", "2. Banana is yellow"], ReadLines(files.Output));
    }

    [Fact]
    public void Sort_SamePath_Throws()
    {
        using var files = new TempSortFiles();
        File.WriteAllText(files.Input, "1. Apple\n");

        Assert.Throws<ArgumentException>(
            () => _sorter.Sort(new SortOptions
            {
                InputPath = files.Input,
                OutputPath = files.Input,
            }));
    }

    [Fact]
    public void Sort_DeletesTemporaryRuns()
    {
        using var files = new TempSortFiles();
        WriteInput(files.Input, "3. C\n1. A\n2. B\n");

        _sorter.Sort(files.Options(chunkSize: 8));

        Assert.True(Directory.Exists(files.Temp));
        Assert.Empty(Directory.GetFiles(files.Temp, "*.run"));
    }

    [Fact]
    public void Sort_WhenRunsExceedMaxFanIn_UsesMultipleMergePasses()
    {
        using var files = new TempSortFiles();
        string[] lines = Enumerable.Range(0, 40)
            .Select(i => $"{40 - i}. Item {i}")
            .ToArray();
        WriteInput(files.Input, string.Join('\n', lines));

        SortResult result = _sorter.Sort(files.Options(chunkSize: 64, maxFanIn: 3));

        Assert.False(result.UsedFastPath);
        Assert.True(result.RunCount > 3, $"Expected more than 3 runs, got {result.RunCount}.");
        Assert.True(result.MergePassCount >= 2, $"Expected at least 2 merge passes, got {result.MergePassCount}.");
        Assert.Equal(Oracle(lines), ReadLines(files.Output));
        Assert.Empty(Directory.GetFiles(files.Temp, "*.run"));
    }

    [Fact]
    public void Sort_WhenFileFitsInChunk_WritesOutputWithoutTempRuns()
    {
        using var files = new TempSortFiles();
        WriteInput(files.Input, "3. C\n1. A\n2. B");

        SortResult result = _sorter.Sort(files.Options(chunkSize: 1024));

        Assert.True(result.UsedFastPath);
        Assert.Equal(0, result.RunCount);
        Assert.Equal(0, result.MergePassCount);
        Assert.Equal(["1. A", "2. B", "3. C"], ReadLines(files.Output));
        Assert.False(Directory.Exists(files.Temp));
    }

    [Fact]
    public void Sort_ParallelPhase1_MatchesSequentialOutput()
    {
        using var files = new TempSortFiles();
        string[] lines = Enumerable.Range(0, 30).Select(i => $"{30 - i}. Item {i}").ToArray();
        WriteInput(files.Input, string.Join('\n', lines));

        string sequentialOutput = files.Output + ".seq";
        _sorter.Sort(new SortOptions
        {
            InputPath = files.Input,
            OutputPath = sequentialOutput,
            TempDirectory = files.Temp + "-seq",
            ChunkSize = 32,
            DegreeOfParallelism = 1,
        });

        _sorter.Sort(files.Options(chunkSize: 32, degreeOfParallelism: 4));

        Assert.Equal(File.ReadAllBytes(sequentialOutput), File.ReadAllBytes(files.Output));
        File.Delete(sequentialOutput);
    }

    [Fact]
    public void Sort_CancelledToken_Throws()
    {
        using var files = new TempSortFiles();
        WriteInput(files.Input, "2. B\n1. A");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            _sorter.Sort(new SortOptions
            {
                InputPath = files.Input,
                OutputPath = files.Output,
                ChunkSize = 8,
                CancellationToken = cts.Token,
            }));
    }

    private static void WriteInput(string path, string content)
    {
        string normalized = content.Replace("\r\n", "\n");
        if (!normalized.EndsWith('\n'))
        {
            normalized += "\n";
        }

        File.WriteAllText(path, normalized, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string[] ReadLines(string path)
    {
        using var reader = new ChunkReader(File.OpenRead(path), bufferSize: 1024, maxLineLength: 1024);
        var lines = new List<string>();

        while (reader.MoveNextChunk())
        {
            foreach (LineRef line in reader.Lines)
            {
                string number = Encoding.UTF8.GetString(line.Number(reader.Buffer));
                string text = Encoding.UTF8.GetString(line.Text(reader.Buffer));
                lines.Add($"{number}. {text}");
            }
        }

        return [.. lines];
    }

    private static string[] Oracle(IEnumerable<string> lines) =>
        lines.Order(Comparer<string>.Create(static (left, right) =>
            LineComparer.CompareLines(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right)))).ToArray();

    private sealed class TempSortFiles : IDisposable
    {
        public TempSortFiles()
        {
            string root = Path.Combine(Path.GetTempPath(), $"lfs-sort-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            Input = Path.Combine(root, "input.txt");
            Output = Path.Combine(root, "output.txt");
            Temp = Path.Combine(root, "tmp");
            Root = root;
        }

        public string Root { get; }
        public string Input { get; }
        public string Output { get; }
        public string Temp { get; }

        public SortOptions Options(
            int chunkSize = 1024,
            int maxFanIn = 64,
            int maxLineLength = 1024,
            int degreeOfParallelism = 1) => new()
        {
            InputPath = Input,
            OutputPath = Output,
            TempDirectory = Temp,
            ChunkSize = chunkSize,
            MaxFanIn = maxFanIn,
            MaxLineLength = maxLineLength,
            DegreeOfParallelism = degreeOfParallelism,
        };

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
