using System.ComponentModel.DataAnnotations;
using Gravelio.Api.Auth;
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
}
