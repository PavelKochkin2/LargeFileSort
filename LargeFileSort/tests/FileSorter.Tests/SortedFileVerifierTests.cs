using FileSorter.Verification;

namespace FileSorter.Tests;

public class SortedFileVerifierTests
{
    [Fact]
    public void Verify_AcceptsSortedPermutation()
    {
        string input = Write("2. Banana\n1. Apple\n");
        string output = Write("1. Apple\n2. Banana\n");

        try
        {
            SortedFileVerifier.Verify(input, output);
        }
        finally
        {
            File.Delete(input);
            File.Delete(output);
        }
    }

    [Fact]
    public void Verify_RejectsUnsortedOutput()
    {
        string input = Write("1. Apple\n2. Banana\n");
        string output = Write("2. Banana\n1. Apple\n");

        try
        {
            Assert.Throws<InvalidDataException>(() => SortedFileVerifier.Verify(input, output));
        }
        finally
        {
            File.Delete(input);
            File.Delete(output);
        }
    }

    [Fact]
    public void Verify_RejectsMissingLine()
    {
        string input = Write("1. Apple\n2. Banana\n3. Cherry\n");
        string output = Write("1. Apple\n3. Cherry\n");

        try
        {
            Assert.Throws<InvalidDataException>(() => SortedFileVerifier.Verify(input, output));
        }
        finally
        {
            File.Delete(input);
            File.Delete(output);
        }
    }

    private static string Write(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), $"lfs-verify-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, content);
        return path;
    }
}
