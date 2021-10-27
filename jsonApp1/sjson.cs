using System;
using System.Text.Json;
using System.Text.Json.Serialization;

public class Headers
{
    [JsonPropertyName("X-Stripe-Client-User-Agent")]
    public string XStripeClientUserAgent { get; set; }
    public string Host { get; set; }

    [JsonPropertyName("Accept-Encoding")]
    public string AcceptEncoding { get; set; }
    public string Authorization { get; set; }

    [JsonPropertyName("Content-Type")]
    public string ContentType { get; set; }
    public string Accept { get; set; }

    [JsonPropertyName("User-Agent")]
    public string UserAgent { get; set; }

    [JsonPropertyName("Content-Length")]
    public string ContentLength { get; set; }

    [JsonPropertyName("Access-Control-Max-Age")]
    public string AccessControlMaxAge { get; set; }

    [JsonPropertyName("Request-Id")]
    public string RequestId { get; set; }

    [JsonPropertyName("Strict-Transport-Security")]
    public string StrictTransportSecurity { get; set; }

    [JsonPropertyName("Stripe-Version")]
    public string StripeVersion { get; set; }
    public string Server { get; set; }
    public string Connection { get; set; }

    [JsonPropertyName("Cache-Control")]
    public string CacheControl { get; set; }
    public string Date { get; set; }

    [JsonPropertyName("Access-Control-Allow-Credentials")]
    public string AccessControlAllowCredentials { get; set; }

    [JsonPropertyName("Access-Control-Allow-Methods")]
    public string AccessControlAllowMethods { get; set; }

    [JsonPropertyName("Access-Control-Allow-Origin")]
    public string AccessControlAllowOrigin { get; set; }
}

public class Request
{
    public string url { get; set; }
    public Headers headers { get; set; }
    public string body { get; set; }
    public string method { get; set; }
}

public class Response
{
    public string body { get; set; }
    public Headers headers { get; set; }
    public int code { get; set; }
}

public class SJson
{
    public Request request { get; set; }
    public Response response { get; set; }
}


