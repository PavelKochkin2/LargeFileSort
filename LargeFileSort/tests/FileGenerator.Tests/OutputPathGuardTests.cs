using FileGenerator.Cli;

namespace FileGenerator.Tests;

public class OutputPathGuardTests
{
    [Fact]
    public void CanWrite_MissingFile_AllowsWrite()
    {
        string path = Path.Combine(Path.GetTempPath(), $"lfs-missing-{Guid.NewGuid():N}.txt");

        Assert.True(OutputPathGuard.CanWrite(path, force: false));
    }

    [Fact]
    public void CanWrite_ExistingFile_WithoutForce_DeniesWrite()
    {
        string path = Path.Combine(Path.GetTempPath(), $"lfs-exists-{Guid.NewGuid():N}.txt");

        try
        {
            File.WriteAllText(path, "keep");

            Assert.False(OutputPathGuard.CanWrite(path, force: false));
            Assert.Equal("keep", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CanWrite_ExistingFile_WithForce_AllowsWrite()
    {
        string path = Path.Combine(Path.GetTempPath(), $"lfs-force-{Guid.NewGuid():N}.txt");

        try
        {
            File.WriteAllText(path, "old");

            Assert.True(OutputPathGuard.CanWrite(path, force: true));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
