using System;
using Vertigo.Fortune.Domain;

namespace Vertigo.Fortune.Presentation
{
    /// <summary>Production random source. Tests supply their own deterministic implementation.</summary>
    public sealed class SystemRandomSource : IRandomSource
    {
        readonly Random random;
        public SystemRandomSource() { random = new Random(); }
        public SystemRandomSource(int seed) { random = new Random(seed); }
        public int Next(int maxExclusive) => random.Next(maxExclusive);
    }
}
