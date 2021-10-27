using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Net.Http;
// using System.Net.Http.Headers;

class WebApiApp1
{
    static readonly HttpClient client = new HttpClient();
    static readonly string url = "https://api.github.com/orgs/dotnet/repos";
    static void Main()
    {

        client.DefaultRequestHeaders.Clear();
        //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        // github API requires user-agent
        client.DefaultRequestHeaders.Add("User-Agent", "dotnet http client ");
        var response = client.GetStringAsync(url).GetAwaiter().GetResult();
        var rps = JsonSerializer.Deserialize<List<Repository>>(response);

        foreach (var r in rps)
        {
            Console.WriteLine($"name: {r.name}");
            Console.WriteLine($"uri: {r.html_url}");
            //Console.WriteLine(r.GitHubHomeUrl);
            //Console.WriteLine(r.JsonDate);
        }

    }
}

public class Repository
{
    [JsonPropertyName("name")]
    public string? name { get; set; }
    public string? html_url { get; set; }

    //[JsonPropertyName("html_url")]
    //public Uri? GitHubHomeUrl { get; set; }

    //[JsonPropertyName("pushed_at")]
    //public string? JsonDate { get; set; }
}