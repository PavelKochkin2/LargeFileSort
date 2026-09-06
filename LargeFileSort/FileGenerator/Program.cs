using FileGenerator;
using FileGenerator.Cli;

try
{
    GeneratorOptions options = GeneratorCli.Parse(args);

    if (File.Exists(options.OutputPath) && !options.Force)
    {
        Console.Error.WriteLine($"File '{options.OutputPath}' already exists. Use --force to overwrite.");
        return 1;
    }

    Console.WriteLine($"Seed: {options.Seed}");

    var factory = new LineFactory(options.UniqueStringCount, options.MaxNumber, options.Seed);
    new FileWriter().Write(options.OutputPath, options.SizeInBytes, factory);

    Console.WriteLine($"Wrote {new FileInfo(options.OutputPath).Length} bytes to {options.OutputPath}");
    return 0;
}
catch (Exception ex) when (ex is ArgumentException or FormatException or IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
