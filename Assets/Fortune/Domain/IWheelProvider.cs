using System.Collections.Generic;

namespace Vertigo.Fortune.Domain
{
    /// <summary>Supplies authored or generated slices, in the same order as the visual wheel.</summary>
    public interface IWheelProvider
    {
        IReadOnlyList<WheelSlice> GetSlices(int zone);
    }

    /// <summary>A random integer in [0, maxExclusive). Inject a deterministic implementation in tests.</summary>
    public interface IRandomSource
    {
        int Next(int maxExclusive);
    }
}
