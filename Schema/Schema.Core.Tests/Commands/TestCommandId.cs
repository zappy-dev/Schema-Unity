using System;
using NUnit.Framework;
using Schema.Core.Commands;

namespace Schema.Core.Tests.Commands;

[TestFixture]
public class TestCommandId
{
    [Test]
    public void Equality_WithSameGuid_ShouldBeEqual()
    {
        var guid = Guid.NewGuid();
        var a = CommandId.FromGuid(guid);
        var b = CommandId.FromGuid(guid);

        Assert.That(a, Is.EqualTo(b));
        Assert.That(a == b, Is.True);
        Assert.That(a != b, Is.False);
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }

    [Test]
    public void Equality_WithDifferentGuids_ShouldNotBeEqual()
    {
        var a = CommandId.NewId();
        var b = CommandId.NewId();

        Assert.That(a, Is.Not.EqualTo(b));
        Assert.That(a == b, Is.False);
        Assert.That(a != b, Is.True);
    }

    [Test]
    public void ToString_ShouldReturnShortEightCharacterId()
    {
        var id = CommandId.NewId();
        var str = id.ToString();
        Assert.That(str, Is.EqualTo(id.ToGuid().ToString("N").Substring(0, 8)));
        Assert.That(str.Length, Is.EqualTo(8));
    }

    [Test]
    public void ImplicitGuidConversions_ShouldRoundTrip()
    {
        var originalId = CommandId.NewId();
        Guid guid = originalId; // implicit conversion to Guid
        CommandId convertedBack = guid; // implicit conversion back to CommandId
        Assert.That(convertedBack, Is.EqualTo(originalId));
    }
} 