using System.ComponentModel.DataAnnotations;

namespace Gravelio.Api.Settings;

/// The map tiles the browser draws maps from. Every map view asks the tile server for the tiles of the area shown, so the public
/// OpenStreetMap server learns where people ride; a self-hoster can point this at their own server. The web image's content-security
/// policy must allow the same origin: <c>TILE_URL</c> feeds both (see docker-compose.yml).
public sealed class TileOptions
{
    public const string DefaultUrl = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";
    public const string DefaultAttribution = "© OpenStreetMap contributors";

    /// Docker Compose passes unset variables as empty strings, so blank means "use the default".
    [RegularExpression(@"^https?://[^/\s;{}]+/\S*\{z\}\S*\{x\}\S*\{y\}\S*$", ErrorMessage = "The tile URL must be an http(s) address containing {{z}}, {{x}} and {{y}}.")]
    public string UrlTemplate
    {
        get;
        set => field = string.IsNullOrWhiteSpace(value) ? DefaultUrl : value.Trim();
    } = DefaultUrl;

    [MaxLength(300)]
    public string Attribution
    {
        get;
        set => field = string.IsNullOrWhiteSpace(value) ? DefaultAttribution : value.Trim();
    } = DefaultAttribution;
}
