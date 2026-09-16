using NUnit.Framework;
using UnityEngine.InputSystem;

public sealed class FlightKeyBindingsTests
{
    [Test]
    public void ParseKey_ReadsBackquoteAndFallback()
    {
        Assert.AreEqual(
            Key.Backquote,
            FlightKeyBindings.ParseKey("Backquote", Key.Space)
        );
        Assert.AreEqual(
            Key.N,
            FlightKeyBindings.ParseKey("n", Key.Backquote)
        );
        Assert.AreEqual(
            Key.Backquote,
            FlightKeyBindings.ParseKey("", Key.Backquote)
        );
        Assert.AreEqual(
            Key.Backquote,
            FlightKeyBindings.ParseKey("NotAKey", Key.Backquote)
        );
    }

    [Test]
    public void FormatKey_UsesGlyphForBackquote()
    {
        Assert.AreEqual("`", FlightKeyBindings.FormatKey(Key.Backquote));
        Assert.AreEqual("N", FlightKeyBindings.FormatKey(Key.N));
    }
}
