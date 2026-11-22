namespace ElansaryWordLibrary.Models;

public record SearchResult(
    string FilePath,
    int ParagraphIndex,
    string Snippet,
    string FullText
);
