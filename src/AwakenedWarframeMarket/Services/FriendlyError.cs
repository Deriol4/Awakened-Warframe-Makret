using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace AwakenedWarframeMarket.Services;

/// <summary>Turns exceptions into messages a player can act on, instead of raw .NET error text.</summary>
public static class FriendlyError
{
    public static string Describe(Exception ex) => ex switch
    {
        HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } =>
            L.RateLimited,
        HttpRequestException { StatusCode: HttpStatusCode.NotFound } =>
            L.ItemNotOnMarket,
        HttpRequestException { StatusCode: >= HttpStatusCode.InternalServerError } =>
            L.MarketProblems,
        HttpRequestException =>
            L.NoConnection,
        TaskCanceledException or TimeoutException =>
            L.TooSlow,
        JsonException =>
            L.UnknownFormat,
        _ => L.Unexpected(ex.Message)
    };
}
