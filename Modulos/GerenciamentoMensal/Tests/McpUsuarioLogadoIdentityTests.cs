using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using WebApi.Interceptor;
using Xunit;

namespace Tests;

public class McpUsuarioLogadoIdentityTests
{
    [Fact]
    public void Traditional_jwt_id_remains_valid_outside_mcp_transport()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("Id", "traditional-owner")], "Bearer"))
        };
        var accessor = new HttpContextAccessor { HttpContext = context };
        var user = new UsuarioLogado(accessor, null!, null!);

        Assert.Equal("traditional-owner", user.Id);
    }

    [Fact]
    public void OAuth_subject_is_used_as_individual_owner_without_shared_header()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", "owner-a")], "McpBearer"))
        };
        var accessor = new HttpContextAccessor { HttpContext = context };
        var user = new UsuarioLogado(accessor, null!, null!);

        Assert.Equal("owner-a", user.Id);
        Assert.Equal("owner-a", user.IdContextoDados);
        Assert.False(user.EmModoCompartilhado);
    }
}
