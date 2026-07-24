using System.Globalization;
using System.Text;

namespace CConner100.RichEditBoxLite;

public sealed record SpellingError(int Start, int Length, string Word, IReadOnlyList<string> Suggestions);

public sealed class SpellCheckService
{
    private readonly Dictionary<string, HashSet<string>> _dictionaries = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en-US"] = new(StringComparer.OrdinalIgnoreCase),
        ["es-ES"] = new(StringComparer.OrdinalIgnoreCase)
    };
    private readonly HashSet<string> _ignored = new(StringComparer.OrdinalIgnoreCase);

    public SpellCheckService()
    {
        LoadBuiltIn("en-US", EnglishWords);
        LoadBuiltIn("es-ES", SpanishWords);
    }

    public void Ignore(string word) => _ignored.Add(word);
    public void AddWord(string languageTag, string word) => GetDictionary(languageTag).Add(word);

    public IReadOnlyList<SpellingError> Check(string text, string languageTag)
    {
        var dictionary = GetDictionary(languageTag);
        var errors = new List<SpellingError>();
        for (var index = 0; index < text.Length;)
        {
            while (index < text.Length && !char.IsLetter(text[index])) index++;
            var start = index;
            while (index < text.Length && (char.IsLetter(text[index]) || text[index] is '\'' or '’')) index++;
            if (index <= start) continue;
            var word = text[start..index];
            if (word.Length > 1 && !_ignored.Contains(word) && !dictionary.Contains(word))
            {
                errors.Add(new SpellingError(start, word.Length, word, Suggest(dictionary, word)));
            }
        }
        return errors;
    }

    public IReadOnlyList<string> Suggest(string word, string languageTag) => Suggest(GetDictionary(languageTag), word);

    private static IReadOnlyList<string> Suggest(IEnumerable<string> dictionary, string word) =>
        dictionary.Where(candidate => Math.Abs(candidate.Length - word.Length) <= 2)
            .Select(candidate => (candidate, distance: Distance(candidate, word)))
            .Where(item => item.distance <= 2)
            .OrderBy(item => item.distance)
            .ThenBy(item => item.candidate, StringComparer.CurrentCultureIgnoreCase)
            .Take(5)
            .Select(item => item.candidate)
            .ToArray();

    private HashSet<string> GetDictionary(string languageTag) =>
        _dictionaries.TryGetValue(languageTag, out var dictionary) ? dictionary : _dictionaries["en-US"];

    private void LoadBuiltIn(string languageTag, string words)
    {
        foreach (var word in words.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            _dictionaries[languageTag].Add(word.Normalize(NormalizationForm.FormC));
        }
    }

    private static int Distance(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        for (var i = 1; i <= left.Length; i++)
        {
            var current = new int[right.Length + 1];
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + (char.ToUpperInvariant(left[i - 1]) == char.ToUpperInvariant(right[j - 1]) ? 0 : 1));
            }
            previous = current;
        }
        return previous[^1];
    }

    private const string EnglishWords =
        "a about after all also an and application are as at be because been before but by can control copy cut document editor event for format from has have hello image in input is it language line link list load notes of on or paragraph paste property range rich save selection should spell table test text that the this to undo use value was we with word world wrap";

    private const string SpanishWords =
        "a acerca además al algo antes aplicación aquí así bien cada cómo con control copiar cortar cuando de del documento dónde editor el ella en entrada es esta este evento formato guardar ha hola imagen la las línea lista los más no notas o para párrafo pegar pingüino por propiedad qué rango selección sí sin sobre tabla también texto tiene un una usar valor y";
}
