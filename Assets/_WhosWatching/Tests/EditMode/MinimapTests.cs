using NUnit.Framework;

namespace WhosWatching.Tests
{
    public class MinimapTests
    {
        [Test] public void ShowsSelfAndTeammatesOnly()
        {
            Assert.That(Minimap.Shows(TeamId.A, true, TeamId.A), Is.True);
            Assert.That(Minimap.Shows(TeamId.A, false, TeamId.A), Is.True);
            Assert.That(Minimap.Shows(TeamId.A, false, TeamId.B), Is.False);
            Assert.That(Minimap.Shows(TeamId.B, false, TeamId.A), Is.False);
            Assert.That(Minimap.Shows(TeamId.None, false, TeamId.None), Is.False);
            Assert.That(Minimap.Shows(TeamId.None, true, TeamId.None), Is.True);
        }
    }
}
