namespace FileGenerator;

public readonly record struct GeneratedLine(int Number, string Text)
{
    public string ToFileText() => $"{Number}. {Text}";
}
