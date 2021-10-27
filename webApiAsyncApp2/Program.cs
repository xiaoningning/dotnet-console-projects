using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

class WebApiApp2
{
    static readonly HttpClientHandler handler = new HttpClientHandler()
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    };
    static readonly HttpClient client = new HttpClient(handler);
    static async Task Main()
    {
        await SendPostJson();
        // await SendPost();
        var rps = await GetGitRepos();
        foreach (var r in rps.Take(2)) Console.WriteLine($"git repo: {r.GitHubHomeUrl}");
    }

    static async Task<List<Repository>> GetGitRepos()
    {
        string url = "https://api.github.com/orgs/dotnet/repos";
        client.DefaultRequestHeaders.Clear();
        // github API requires user-agent
        client.DefaultRequestHeaders.Add("User-Agent", "dotnet core http client");
        // GetStreamAsync instead of GetStringAsync
        var responseTask = client.GetAsync(url);
        var res = await responseTask;
        var rps = JsonSerializer.Deserialize<List<Repository>>(await res.Content.ReadAsStringAsync());
        return rps;
    }
    static async Task SendPostJson()
    {
        var bodyObj = new JBody()
        {
            Id = 78912,
            Customer = "Jason Sweet",
            Quantity = 1,
            Price = 18.00
        };
        var jsonStr = JsonSerializer.Serialize<JBody>(bodyObj);
        var bodyNoClass = new
        {
            Id = 78912,
            Customer = "Jason Sweet",
            Quantity = 1,
            Price = 18.00
        };
        jsonStr = JsonSerializer.Serialize(bodyNoClass);
        Console.WriteLine("httpclient post as stringcontent");
        var content = new StringContent(jsonStr, Encoding.UTF8, "application/json");
        var responseTask = client.PostAsync("https://reqbin.com/echo/post/json", content);
        // dotnet 6+ := task.WaitAsync(timeout)
        var res = await responseTask.WaitAsync(TimeSpan.FromSeconds(5));
        res.EnsureSuccessStatusCode();
        var resStr = await res.Content.ReadAsStringAsync();
        Console.WriteLine(resStr);

        Console.WriteLine("httpclient post as json object");
        // var responseJsonTask = client.PostAsJsonAsync("https://reqbin.com/echo/post/json", bodyNoClass);
        var responseJsonTask = client.PostAsJsonAsync<JBody>("https://reqbin.com/echo/post/json", bodyObj);

        var resJson = await responseJsonTask;
        resJson.EnsureSuccessStatusCode();
        Console.WriteLine("httpclient post response read as string");
        var resJsonStr = await res.Content.ReadAsStringAsync();
        Console.WriteLine(resJsonStr);

        // JsonNode := dotnet 6+
        Console.WriteLine("httpclient post response read as JsonNode, dotnet 6+");
        var resJsonObj = await res.Content.ReadFromJsonAsync<JsonNode>();
        Console.WriteLine(resJsonObj["success"]);
    }

    public class JBody
    {
        public int Id { get; set; }
        public string Customer { get; set; }
        public int Quantity { get; set; }
        public double Price { get; set; }
    }
    static async Task SendPost()
    {
        client.DefaultRequestHeaders.Clear();
        string url = "https://api.test.appcloud.com";
        client.BaseAddress = new Uri(url);

        //Basic Authentication
        string clientId = "4eC39HqLyjWDarjtT1zdp7dc_test_";
        string clientSecret = "";
        var authenticationString = $"{clientId}:{clientSecret}";
        // it must be UTF8 encoding, ASCII encoding does not work!!!
        var base64EncodedAuthenticationString = Convert.ToBase64String(ASCIIEncoding.UTF8.GetBytes(authenticationString));
        // client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", $"{base64EncodedAuthenticationString}");
        // Bearer with Client Id works as well !!!
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"{clientId}");

        // create a charge
        var values = new List<KeyValuePair<string, string>>();
        values.Add(new KeyValuePair<string, string>("amt", "200"));
        values.Add(new KeyValuePair<string, string>("cncy", "usd"));
        values.Add(new KeyValuePair<string, string>("source", "helloworld"));
        var content = new FormUrlEncodedContent(values);
        var responseTask = client.PostAsync("https://api.test.appcloud.com/v1/xyz", content);
        var res = await responseTask;
        res.EnsureSuccessStatusCode();
        string resBody = await res.Content.ReadAsStringAsync();
        Console.WriteLine(resBody);
        var c1 = JsonSerializer.Deserialize<Xyz>(resBody);

        int cnt = 3;
        string query = $"?limit={cnt}";
        responseTask = client.GetAsync($"https://api.test.appcloud.com/v1/xyz{query}");
        res = await responseTask;
        resBody = await res.Content.ReadAsStringAsync();
        Console.WriteLine(resBody);
        var lcs = JsonSerializer.Deserialize<ListXyz>(resBody);
    }
}

public class Xyz
{
    [JsonPropertyName("id")]
    public string id { get; set; }
    [JsonPropertyName("amount")]
    public int amount { get; set; }
    [JsonPropertyName("invoice")]
    public string invoice { get; set; }
    [JsonPropertyName("customer")]
    public string customer { get; set; }
}

public class ListXyz
{
    public List<Xyz> data { get; set; }
    public bool has_more { get; set; }
    public string url { get; set; }
    // object is key word in c#, add "@"
    public string @object { get; set; }
}
public class Repository
{
    [JsonPropertyName("name")]
    public string name { get; set; }
    // public string html_url { get; set; }

    [JsonPropertyName("html_url")]
    public Uri GitHubHomeUrl { get; set; }

    [JsonPropertyName("pushed_at")]
    public string JsonDate { get; set; }
}