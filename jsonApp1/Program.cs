using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO;
using System.Collections.Generic;

class JsonApp1
{
    static readonly string fileName = "jsonApp1.json";
    static void Main(string[] args)
    {
        string jstr2 = "";
        using (StreamReader sr = File.OpenText("sr-2.json"))
        {
            jstr2 = sr.ReadToEnd();
        }
        var j2 = JsonSerializer.Deserialize<List<SJson>>(jstr2);

        var p1 = new Person("a");
        p1.Ids.Add(1);
        p1.Groups?.Add(1, "g1");

        var p2 = new Person("b");
        p2.Ids.Add(2);
        p2.Groups?.Add(2, "g2");

        var pList = new List<Person>() { p1, p2 };
        Console.WriteLine($"{JsonSerializer.Serialize<Person>(p1)}");
        var jsonStr = JsonSerializer.Serialize<List<Person>>(pList);
        Console.WriteLine($"{jsonStr}");

        // Converting null literal or possible null value to non-nullable type
        List<Person> oList = JsonSerializer.Deserialize<List<Person>>(jsonStr);
        Console.WriteLine($"{oList?.Count}");
        if (oList != null) foreach (var x in oList) Console.WriteLine($"{x}");

        using (StreamWriter sw = File.CreateText(fileName))
        {
            sw.WriteLine(jsonStr);
        }
        string fileStr;
        using (StreamReader sr = File.OpenText(fileName))
        {
            fileStr = sr.ReadLine();
        }
        Console.WriteLine($"json from file");
        List<Person> fList = JsonSerializer.Deserialize<List<Person>>(fileStr);
        if (fList != null) foreach (var x in fList) Console.WriteLine($"{x}");

        Console.WriteLine("jsondocument parse str to object withou deserializer");

        // jsondocument/jsonelement in dotnet 5+ or dotnet core 3+
        string json_text = "{\"foo\": {\"bar\": [{\"p1\": \"a\"}, {\"p1\": \"b\"}, {\"p1\": \"c\"}]}}";
        var jdoc = JsonDocument.Parse(json_text);
        foreach (var ex in jdoc.RootElement.GetProperty("foo").GetProperty("bar").EnumerateArray())
        {
            var pv = ex.GetProperty("p1").GetString();
            Console.WriteLine($"foo->bar->p1: {pv}");
        }
        Console.WriteLine(jdoc.RootElement.ToString());

        var o1 = new { o1p1 = "o1p1", vals = new[] { "a", "b" }, ids = new[] { 1, 2 } };
        var o1JsonStr = JsonSerializer.Serialize(o1);
        Console.WriteLine(o1JsonStr);

        // jsonnode in dotnet 6+; similar to jobject in newton.json
        var fooNode = JsonNode.Parse(json_text);
        var options = new JsonSerializerOptions { WriteIndented = true };
        Console.WriteLine(fooNode.ToJsonString(options));
        Console.WriteLine(fooNode["foo"]["bar"][1]["p1"]);
        var bars = fooNode["foo"]["bar"].AsArray();
        foreach (var b in bars) Console.WriteLine($"jsonnode as array: {b["p1"]}");
        fooNode["foo"].AsObject().Add("newp1", new JsonObject { ["yes"] = 60 });
        fooNode["foo"].AsObject().Add("newArrayP2", new JsonArray { new JsonObject { ["p2"] = 6 }, new JsonObject { ["p2"] = 16 } });
        string fooNodeJsonStr = fooNode.ToJsonString(options);
        Console.WriteLine(fooNodeJsonStr);
    }
}

public class Person
{
    public string Name { get; set; }
    public List<int> Ids { get; set; }
    public Dictionary<int, string> Groups { get; set; }
    public Person() { }
    public Person(string n)
    {
        Ids = new List<int>();
        Name = n;
        Groups = new Dictionary<int, string>();
    }
    public override string ToString()
    {
        if (Name == null) return string.Format("null");
        else return string.Format($"{Name} {string.Join(",", Groups.Values)}");
    }
}