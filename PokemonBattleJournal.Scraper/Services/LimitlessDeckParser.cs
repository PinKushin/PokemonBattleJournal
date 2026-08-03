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
            IHtmlCollection<IElement> imgs = row.QuerySelectorAll("img.pokemon");
            IElement? img = imgs.Length > 0 ? imgs[0] : null;

            if (anchor is null || img is null)
                continue;

            // Strip annotation span text from anchor text to get base name
            IElement? annotation = anchor.QuerySelector("span.annotation");
            string annotationText = annotation?.TextContent?.Trim() ?? string.Empty;
            string fullText = anchor.TextContent.Trim();

            string name;
            if (annotationText.Length > 0)
            {
                string baseName = fullText.Replace(annotationText, string.Empty).Trim();
                name = $"{baseName} {annotationText}".Trim();
            }
            else
            {
                name = fullText;
            }

            string imageUrl = img.GetAttribute("src") ?? string.Empty;
            string? secondaryImageUrl = imgs.Length > 1 ? imgs[1].GetAttribute("src") : null;

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(imageUrl))
                continue;

            decks.Add(new MetaDeck(name, imageUrl, secondaryImageUrl));
        }

        return decks;
    }
}
