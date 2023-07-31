using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OAuthWebCore.extensions;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection.PortableExecutable;
using System.Security.Claims;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;
using RestSharp;
using RestSharp.Authenticators;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthorization();

builder.Services.AddAuthentication("cookie")
    .AddCookie("cookie")
    .AddOAuth("github", o =>
    {
        o.SignInScheme = "cookie";

        var config = builder.Configuration;
        o.ClientId = config["Authentication:Github:ClientId"] ?? ""; 
        o.ClientSecret = config["Authentication:Github:ClientSecret"] ?? ""; 

        
        o.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
        o.TokenEndpoint = "https://github.com/login/oauth/access_token";
        o.CallbackPath = "/oauth/github-cb";
        o.SaveTokens = true;
        o.UserInformationEndpoint = "https://api.github.com/user";

        o.ClaimActions.MapJsonKey("sub", "id");
        o.ClaimActions.MapJsonKey(ClaimTypes.Name, "login");
        o.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");

        o.Events.OnCreatingTicket = async ctx =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ctx.Options.UserInformationEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.AccessToken);
            using var result  = await ctx.Backchannel.SendAsync(request);
            var user = await result.Content.ReadFromJsonAsync<JsonElement>();
            ctx.RunClaimActions(user);
        };

    });

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", (HttpContext ctx) =>
{
    ctx.GetTokenAsync("access_token");
    string combinedString = string.Join(",", ctx.User.Claims.Select(x => new { x.Type, x.Value }).ToList());
    var page = "<html><body>"
        + combinedString
        + "<br><a href=/login>login</a> "
        + " <br><a href=/logout>logout</a> "
        + " <br><a href=/api/test>api test</a> "
        + "</body></html>";
    return Results.Text(page, "text/html");
});

// login path
app.MapGet("/login", () => { 
    return Results.Challenge(
        new AuthenticationProperties()
        {
            RedirectUri = "https://localhost:5005/"
        },
    authenticationSchemes: new List<string>() { "github" }); 
});

app.MapGet("/logout", async (HttpContext ctx) =>
{
    var config = builder.Configuration;
    var clientId = config["Authentication:Github:ClientId"] ?? "";
    var clientSecret = config["Authentication:Github:ClientSecret"] ?? "";
    var accessToken = ctx.GetTokenAsync("access_token").Result;
    var requestUri = new Uri($"https://api.github.com/applications/{clientId}/token");

    if (accessToken != null)
    {
        var options = new RestClientOptions()
        {
            Authenticator = new HttpBasicAuthenticator(clientId, clientSecret),
            BaseUrl = requestUri,
        };
        var reqq = new RestRequest().AddJsonBody(new { access_token = accessToken });
        var client = new RestClient(options);
        var response = await client.DeleteAsync(reqq);
        if (response.IsSuccessStatusCode)
        {
            await ctx.SignOutAsync("cookie");
        }

        var body = response.StatusDescription + " / " +
            response.StatusCode.ToString() + " / " +
            response.ResponseStatus.ToString() + " / " +
            response.Content;

        return Results.Redirect("/");
    }
    return Results.Text("No access token!", "text/html");
});

app.Map("/api/test", (HttpContext ctx) =>
{
    if (!ctx.User!.Identity!.IsAuthenticated )
    {
        return Results.Ok("Logged out");
    }
    if (ctx.User?.Claims?.ToList().Count > 0)
    {
        string claims = string.Join(",", ctx.User.Claims.ToList());
        return Results.Ok("Logged in with claims:" + claims);
    }
    return Results.Ok("Logged in but no claims!");
});

app.Run();
