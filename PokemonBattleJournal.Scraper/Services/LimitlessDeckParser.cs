using AngleSharp;
using AngleSharp.Dom;
using PokemonBattleJournal.Scraper.Interfaces;
using PokemonBattleJournal.Scraper.Models;

namespace PokemonBattleJournal.Scraper.Services;

public class LimitlessDeckParser : IMetaDeckParser
{
    public List<MetaDeck> Parse(string html, int count)
    {
        if (string.IsNullOrWhiteSpace(html))
            return [];

        IBrowsingContext context = BrowsingContext.New(Configuration.Default);
        IDocument document = context.OpenAsync(req => req.Content(html)).GetAwaiter().GetResult();

        IHtmlCollection<IElement> rows = document.QuerySelectorAll("table tbody tr");
        List<MetaDeck> decks = [];

        foreach (IElement row in rows)
        {
            if (decks.Count >= count)
                break;

            IElement? anchor = row.QuerySelector("td:nth-child(3) a");
            IElement? img = row.QuerySelector("img.pokemon");

            if (anchor is null || img is null)
                continue;

            // Remove the annotation span text from the full anchor text
            IElement? annotation = anchor.QuerySelector("span.annotation");
            string annotationText = annotation?.TextContent?.Trim() ?? string.Empty;
            string fullText = anchor.TextContent.Trim();
            string name = annotationText.Length > 0
                ? fullText.Replace(annotationText, annotationText).Trim()
                : fullText;

            // Rebuild name: base name + annotation (e.g. "Dragapult ex")
            string baseName = fullText.Replace(annotationText, string.Empty).Trim();
            name = annotationText.Length > 0 ? $"{baseName} {annotationText}" : baseName;

            string imageUrl = img.GetAttribute("src") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(imageUrl))
                continue;

            decks.Add(new MetaDeck(name, imageUrl));
        }

        return decks;
    }
}
