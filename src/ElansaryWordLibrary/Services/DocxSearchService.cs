using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ElansaryWordLibrary.Models;

namespace ElansaryWordLibrary.Services;

public class DocxSearchService
{
    private readonly List<string> _files = new();

    public void AddFolder(string folder)
    {
        if (!Directory.Exists(folder)) return;
        _files.AddRange(Directory.EnumerateFiles(folder, "*.docx", SearchOption.AllDirectories));
    }

    public void AddFiles(IEnumerable<string> files)
    {
        _files.AddRange(files.Where(File.Exists));
    }

    public void Clear() => _files.Clear();

    public List<SearchResult> Search(string query)
    {
        var results = new List<SearchResult>();
        var terms = ExtractTerms(query);
        if (terms.Count == 0) return results;

        foreach (var file in _files.Distinct())
        {
            try
            {
                using var document = WordprocessingDocument.Open(file, false);
                var body = document.MainDocumentPart?.Document.Body;
                if (body is null) continue;

                var paragraphs = body.Descendants<Paragraph>().ToList();
                for (int i = 0; i < paragraphs.Count; i++)
                {
                    var text = GetParagraphText(paragraphs[i]);
                    if (Matches(text, terms))
                    {
                        var snippet = text.Length > 180 ? text[..180] + "…" : text;
                        results.Add(new SearchResult(file, i, snippet, text));
                    }
                }
            }
            catch
            {
                // تجاهل الملفات التالفة أو غير القابلة للقراءة
            }
        }

        return results;
    }

    private static List<string> ExtractTerms(string query)
    {
        var terms = new List<string>();
        foreach (Match match in Regex.Matches(query, "\"([^\"]+)\""))
        {
            var phrase = match.Groups[1].Value.Trim();
            if (phrase.Length > 0) terms.Add(phrase);
        }

        var withoutQuotes = Regex.Replace(query, "\"[^\"]*\"", " ");
        terms.AddRange(withoutQuotes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return terms.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool Matches(string text, List<string> terms)
    {
        return terms.All(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetParagraphText(Paragraph paragraph)
    {
        var builder = new StringBuilder();
        foreach (var text in paragraph.Descendants<Text>()) builder.Append(text.Text);
        return builder.ToString().Trim();
    }
}
