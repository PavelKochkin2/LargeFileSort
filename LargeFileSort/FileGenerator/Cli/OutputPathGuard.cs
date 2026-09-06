namespace FileGenerator.Cli;

public static class OutputPathGuard
{
    public static bool CanWrite(string path, bool force) => !File.Exists(path) || force;
}
