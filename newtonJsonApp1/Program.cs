using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/**
newtonesoft.json does not support async out of box
system.text.json supports async. jsondocument/jsonnode is similar to jobject
*/
class NewtonJsonApp
{
    static void Main()
    {
        string fileStr;
        using (StreamReader sr = File.OpenText("person1.json"))
        {
            fileStr = sr.ReadToEnd();
        }
        var ps = JsonConvert.DeserializeObject<List<Person>>(fileStr);
        ps.ForEach(p => { Console.WriteLine($"{p.Name}"); });

        var p1 = new Person("x")
        {
            Ids = new int[] { 4, 5 },
            Groups = new Dictionary<int, string>()
            {
                [4] = "g4",
                [5] = "g5"
            }
        };

        var p1Str = JsonConvert.SerializeObject(p1);
        Console.WriteLine(p1Str);

        string json_text = "{\"foo\": {\"bar\": [{\"p1\": \"a\"}, {\"p1\": \"b\"}, {\"p1\": \"c\"}]}}";
        // it needs to be dynamic object
        dynamic data = JsonConvert.DeserializeObject(json_text);
        string p1val = data.foo.bar[2].p1;  // "c"
        Console.WriteLine(p1val);

        /**
                    {
            "foo": {
                "bar": [
                    {
                        "p1": "a"
                    },
                    {
                        "p1": "b"
                    },
                    {
                        "p1": "c"
                    }
                ],
                "xyz": {
                    "abc": "abc",
                    "vals": [
                        2.718,
                        3.142
                    ]
                }
            }
            }
        */
        data.foo.xyz = JObject.FromObject(
            new
            {
                abc = "abc",
                vals = new[] { 2.718, 3.142 }
            }
        );

        string result = JsonConvert.SerializeObject(data, Formatting.Indented);
        Console.WriteLine(result);
    }
}

public class Person
{
    [JsonProperty("Name")]
    public string Name { get; set; }
    [JsonProperty("Ids")]
    public int[] Ids { get; set; }
    public Dictionary<int, string> Groups { get; set; }
    public Person() { }
    public Person(string n)
    {
        Name = n;
        Groups = new Dictionary<int, string>();
    }
    public override string ToString()
    {
        if (Name == null) return string.Format("null");
        else return string.Format($"{Name} {string.Join(",", Groups.Values)}");
    }
}