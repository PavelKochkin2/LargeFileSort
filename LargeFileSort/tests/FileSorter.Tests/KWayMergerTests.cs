using System.Text;
using FileSorter.Sorting.Merging;
using FileSorter.Sorting.Parsing;

namespace FileSorter.Tests;

public class KWayMergerTests
{
    private readonly KWayMerger _merger = new();

    [Fact]
    public void Merge_OneRun_CopiesLines()
    {
        string run = WriteRun("1. Apple\n415. Apple\n");
        string output = NewTempPath();

        try
        {
            _merger.Merge([run], output, bufferSize: 32);

            Assert.Equal(["1. Apple", "415. Apple"], ReadLines(output));
        }
        finally
        {
            File.Delete(run);
            File.Delete(output);
        }
    }

    [Fact]
    public void Merge_TwoSortedRuns_MatchesAssignmentOrder()
    {
        string first = WriteRun("1. Apple\n415. Apple\n30432. Something something something\n");
        string second = WriteRun("2. Banana is yellow\n32. Cherry is the best\n");
        string output = NewTempPath();

        try
        {
            _merger.Merge([first, second], output, bufferSize: 16);

            Assert.Equal(
                [
                    "1. Apple",
                    "415. Apple",
                    "2. Banana is yellow",
                    "32. Cherry is the best",
                    "30432. Something something something",
                ],
                ReadLines(output));
        }
        finally
        {
            File.Delete(first);
            File.Delete(second);
            File.Delete(output);
        }
    }

    [Fact]
    public void Merge_KeepsDuplicatesFromDifferentRuns()
    {
        string first = WriteRun("1. Apple\n2. Apple\n");
        string second = WriteRun("1. Apple\n3. Apple\n");
        string output = NewTempPath();

        try
        {
            _merger.Merge([first, second], output, bufferSize: 8);

            Assert.Equal(["1. Apple", "1. Apple", "2. Apple", "3. Apple"], ReadLines(output));
        }
        finally
        {
            File.Delete(first);
            File.Delete(second);
            File.Delete(output);
        }
    }

    [Fact]
    public void Merge_SkipsEmptyRuns()
    {
        string empty = WriteRun("");
        string run = WriteRun("1. Apple\n2. Banana\n");
        string otherEmpty = WriteRun("");
        string output = NewTempPath();

        try
        {
            _merger.Merge([empty, run, otherEmpty], output, bufferSize: 8);

            Assert.Equal(["1. Apple", "2. Banana"], ReadLines(output));
        }
        finally
        {
            File.Delete(empty);
            File.Delete(run);
            File.Delete(otherEmpty);
            File.Delete(output);
        }
    }

    [Fact]
    public void Merge_AllEmptyRuns_CreatesEmptyFile()
    {
        string first = WriteRun("");
        string second = WriteRun("");
        string output = NewTempPath();

        try
        {
            _merger.Merge([first, second], output);

            Assert.Equal(0, new FileInfo(output).Length);
        }
        finally
        {
            File.Delete(first);
            File.Delete(second);
            File.Delete(output);
        }
    }

    [Fact]
    public void Merge_ThreeRuns_MatchesOracle()
    {
        string[] runTexts =
        [
            "1. Apple\n9. Apple\n",
            "2. Banana\n7. Banana\n",
            "4. Apple\n3. Cherry\n",
        ];
        string[] runPaths = runTexts.Select(WriteRun).ToArray();
        string output = NewTempPath();

        try
        {
            _merger.Merge(runPaths, output, bufferSize: 8);

            string[] expected = Oracle(runTexts);
            Assert.Equal(expected, ReadLines(output));
        }
        finally
        {
            foreach (string path in runPaths)
            {
                File.Delete(path);
            }

            File.Delete(output);
        }
    }

    private static string[] Oracle(IEnumerable<string> runTexts) =>
        runTexts
            .SelectMany(text => text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n', StringSplitOptions.RemoveEmptyEntries))
            .Order(Comparer<string>.Create(CompareLines))
            .ToArray();

    private static int CompareLines(string left, string right) =>
        LineComparer.CompareLines(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));

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

    private static string WriteRun(string content)
    {
        string path = NewTempPath();
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    private static string NewTempPath() => Path.Combine(Path.GetTempPath(), $"lfs-merge-{Guid.NewGuid():N}.txt");
}
