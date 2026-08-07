namespace PokemonBattleJournal.Services;

/// <summary>
/// Maps CDN image URLs (Limitless/TrainerHill) to local Pokémon Showdown sprite filenames.
/// Limitless CDN and Showdown use different naming conventions for some forms.
/// </summary>
internal static class SpriteResolver
{
    // Limitless CDN names (post hyphen→underscore) that differ from Showdown sprite names.
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // Ogerpon: Limitless omits "_mask"; Showdown includes it
        { "ogerpon.png",             "ogerpon_teal_mask.png" },
        { "ogerpon_wellspring.png",  "ogerpon_wellspring_mask.png" },
        { "ogerpon_hearthflame.png", "ogerpon_hearthflame_mask.png" },
        { "ogerpon_cornerstone.png", "ogerpon_cornerstone_mask.png" },
        // Mega forms Limitless labels that have no Mega in the games
        { "greninja_mega.png", "greninja.png" },
        { "starmie_mega.png",  "starmie.png" },
    };

    /// <summary>
    /// Resolves a CDN image URL to a local sprite filename.
    /// Returns null if the URL is empty or not an absolute URI.
    /// </summary>
    internal static string? FromUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) ||
            !Uri.TryCreate(imageUrl, UriKind.Absolute, out Uri? uri))
            return null;

        string file = Path.GetFileName(uri.AbsolutePath);
        if (string.IsNullOrEmpty(file))
            return null;

        string candidate = file.Replace('-', '_');
        return Aliases.TryGetValue(candidate, out string? alias) ? alias : candidate;
    }
}
