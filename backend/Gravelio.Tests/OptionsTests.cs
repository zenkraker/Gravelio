using System.ComponentModel.DataAnnotations;
using Gravelio.Api.Auth;
using Gravelio.Api.Geocoding;
using Gravelio.Api.Settings;
using Microsoft.Extensions.Configuration;

namespace Gravelio.Tests;

public class OptionsTests
{
    [Fact]
    public void Blank_admin_settings_from_docker_compose_are_treated_as_not_set()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Admin:Email"] = "", ["Admin:Password"] = "" }).Build();
        var options = config.GetSection("Admin").Get<AdminOptions>()!;

        Assert.Null(options.Email);
        Assert.Null(options.Password);
        Assert.True(Validator.TryValidateObject(options, new ValidationContext(options), null, validateAllProperties: true));
    }

    [Fact]
    public void Blank_geocoding_settings_from_docker_compose_mean_the_defaults()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Geocoding:Host"] = "", ["Geocoding:UserAgent"] = "  ", ["Geocoding:Enabled"] = "false" }).Build();
        var options = config.GetSection("Geocoding").Get<GeocodingOptions>()!;

        Assert.Equal("https://nominatim.openstreetmap.org", options.Host);
        Assert.Contains("Gravelio", options.UserAgent);
        Assert.False(options.Enabled);
        Assert.True(Validator.TryValidateObject(options, new ValidationContext(options), null, validateAllProperties: true));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("https://tiles.example.org/{z}/{x}/{y}.png", true)]
    [InlineData("http://tiles.lan:8080/styles/basic/{z}/{x}/{y}.png?key=abc", true)]
    [InlineData("https://tiles.example.org/{z}/{x}.png", false)] // no {y}
    [InlineData("https://{s}.tile.example.org/{z}/{x}/{y}.png", false)] // a subdomain placeholder cannot be allowed by the content-security-policy
    [InlineData("javascript:alert(1)//{z}/{x}/{y}", false)]
    [InlineData("https://evil.example;script-src */{z}/{x}/{y}", false)] // would add a directive to the policy
    public void The_tile_url_must_be_a_web_address_with_the_three_placeholders(string url, bool valid)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Tiles:UrlTemplate"] = url, ["Tiles:Attribution"] = "" }).Build();
        var options = config.GetSection("Tiles").Get<TileOptions>()!;

        Assert.Equal(valid, Validator.TryValidateObject(options, new ValidationContext(options), null, validateAllProperties: true));
        if (url == "") Assert.Equal(TileOptions.DefaultUrl, options.UrlTemplate); // blank from Docker Compose means the default
        Assert.Equal(TileOptions.DefaultAttribution, options.Attribution);
    }
}
