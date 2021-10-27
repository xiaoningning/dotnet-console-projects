using System;
using Xunit;
using lib;

namespace tests;

public class UnitTest1
{
    Class1 class1;
    Hello h1;
    public UnitTest1()
    {
        class1 = new Class1();
        h1 = new Hello()
        {
            Intro = "Hello",
            Place = "World"
        };
    }

    [Fact]
    public void TestJsonNetForSomeReason()
    {

        var jsonStr = class1.UseJsonNetForSomeReason(h1);
        Console.WriteLine($"TestDeJsonNetForSomeReason: {jsonStr}");
        Assert.NotNull(jsonStr);
    }

    [Fact]
    public void TestDeJsonNetForSomeReason()
    {
        var str1 = @"{'Intro':'Hello','Place':'World'}";
        var obj1 = class1.DeJsonNetForSomeReason<Hello>(str1);
        Assert.NotNull(obj1);
        Console.WriteLine($"TestDeJsonNetForSomeReason: {obj1.Intro}");
        Assert.Equal(obj1.Intro, h1.Intro);
    }

    [Fact]
    public void FailingTest()
    {
        Assert.True(false, "failing test");
    }
}