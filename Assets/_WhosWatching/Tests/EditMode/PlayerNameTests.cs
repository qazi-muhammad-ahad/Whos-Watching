using NUnit.Framework;
namespace WhosWatching.Tests
{
    public sealed class PlayerNameTests
    {
        [TestCase(null,"Player")]
        [TestCase("   ","Player")]
        [TestCase("  Boo Bear  ","Boo Bear")]
        [TestCase("<boo>\n!","boo")]
        [TestCase("abcdefghijklmnopqr","abcdefghijklmnop")]
        [TestCase("Devil_2-X","Devil_2-X")]
        public void NamesAreShortPlainText(string input,string expected) => Assert.That(NetworkPlayer.CleanName(input),Is.EqualTo(expected));
    }
}
