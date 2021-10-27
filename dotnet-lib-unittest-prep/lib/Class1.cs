using System;
using Newtonsoft.Json;

namespace lib;

public class Class1
{
    public string UseJsonNetForSomeReason<T>(T input)
    {
        return JsonConvert.SerializeObject(input);
    }
    public T DeJsonNetForSomeReason<T>(string s)
    {
        return JsonConvert.DeserializeObject<T>(s);
    }
}
public class Hello
{
    public string Intro { get; set; }
    public string Place { get; set; }
}
