using System.ComponentModel.DataAnnotations;
using Gravelio.Api.Auth;
using Gravelio.Api.Geocoding;
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
}
