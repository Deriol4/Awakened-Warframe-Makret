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
            "warframe.market is limiting requests right now. Try again in a minute.",
        HttpRequestException { StatusCode: HttpStatusCode.NotFound } =>
            "warframe.market doesn't have this item.",
        HttpRequestException { StatusCode: >= HttpStatusCode.InternalServerError } =>
            "warframe.market is having problems right now. Try again later.",
        HttpRequestException =>
            "Couldn't reach warframe.market. Check your internet connection.",
        TaskCanceledException or TimeoutException =>
            "warframe.market took too long to answer. Try again.",
        JsonException =>
            "warframe.market answered in a format this version doesn't understand. The app may need an update.",
        _ => $"Something unexpected went wrong: {ex.Message}"
    };
}
