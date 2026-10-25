using System.Linq.Expressions;
using Gravelio.Api.Data;

namespace Gravelio.Api.Users;

/// The only user fields another user may see: no email, role, birthdate or body data.
public sealed record PersonRead(int Id, string Name, string Username, string? City)
{
    public static readonly Expression<Func<User, PersonRead>> Projection =
        u => new PersonRead(u.Id, u.Name, u.UserName!, u.City);

    static readonly Func<User, PersonRead> Compiled = Projection.Compile();

    public static PersonRead From(User u) => Compiled(u);
}
