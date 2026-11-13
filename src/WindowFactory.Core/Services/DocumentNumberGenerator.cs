using System.Globalization;
using System.Text.RegularExpressions;

namespace WindowFactory.Core.Services;

public interface IDocumentNumberGenerator
{
    string Prefix(int year);
    string Format(int year, int index);
    int NextIndex(int year, IEnumerable<string> existingNumbers);
    bool TryParse(string number, out int year, out int index);
}

/// <summary>Маска номера документа: ПР23-ОКН-{Год}-{Номер:D5}.</summary>
public sealed class DocumentNumberGenerator : IDocumentNumberGenerator
{
    public const string Code = "ОКН";
    private static readonly Regex Rx = new(@"^ПР23-ОКН-(\d{4})-(\d{5})$", RegexOptions.Compiled);

    public string Prefix(int year) => $"ПР23-{Code}-{year.ToString(CultureInfo.InvariantCulture)}-";

    public string Format(int year, int index) => Prefix(year) + index.ToString("D5", CultureInfo.InvariantCulture);

    public int NextIndex(int year, IEnumerable<string> existingNumbers)
    {
        var max = 0;
        foreach (var n in existingNumbers)
            if (TryParse(n, out var y, out var i) && y == year && i > max) max = i;
        return max + 1;
    }

    public bool TryParse(string number, out int year, out int index)
    {
        year = index = 0;
        var m = Rx.Match(number ?? string.Empty);
        if (!m.Success) return false;
        year = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        index = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        return true;
    }
}
