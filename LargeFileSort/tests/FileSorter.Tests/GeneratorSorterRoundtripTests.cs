using FileGenerator;
using FileSorter.Sorting;
using FileSorter.Verification;

namespace FileSorter.Tests;

public class GeneratorSorterRoundtripTests
{
    [Theory]
    [InlineData(7)]
    [InlineData(13)]
    [InlineData(256)]
    [InlineData(4096)]
    public void GeneratedFile_SortsAndVerifies(long size)
    {
        string input = Path.Combine(Path.GetTempPath(), $"lfs-e2e-in-{Guid.NewGuid():N}.txt");
        string output = Path.Combine(Path.GetTempPath(), $"lfs-e2e-out-{Guid.NewGuid():N}.txt");
        string temp = Path.Combine(Path.GetTempPath(), $"lfs-e2e-tmp-{Guid.NewGuid():N}");

        try
        {
            new FileWriter().Write(input, size, new LineFactory(8, 50, seed: 42));
            Assert.Equal(size, new FileInfo(input).Length);

            new ExternalSorter().Sort(new SortOptions
            {
                InputPath = input,
                OutputPath = output,
                TempDirectory = temp,
                ChunkSize = 64,
                DegreeOfParallelism = 2,
            });

            SortedFileVerifier.Verify(input, output);
        }
        finally
        {
            if (File.Exists(input))
            {
                File.Delete(input);
            }

            if (File.Exists(output))
            {
                File.Delete(output);
            }

            if (Directory.Exists(temp))
            {
                Directory.Delete(temp, recursive: true);
            }
        }
    }
}
