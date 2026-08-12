namespace ePinPong.Services
{
    /// <summary>
    /// Apstrakcija za generisanje slučajnih brojeva.
    /// Omogućava deterministične jedinične testove sa fiksnim seed-om
    /// umjesto direktnog <c>new Random()</c> koji nije kontrolisao iz testova.
    /// </summary>
    public interface IRandomProvider
    {
        /// <summary>Vraća nenegativan slučajan cijeli broj.</summary>
        int Next();

        /// <summary>Vraća slučajan cijeli broj manji od <paramref name="maxValue"/>.</summary>
        int Next(int maxValue);
    }

    /// <summary>
    /// Produkcijska implementacija koja koristi .NET thread-safe <see cref="System.Random.Shared"/>.
    /// </summary>
    public sealed class DefaultRandomProvider : IRandomProvider
    {
        public int Next()             => System.Random.Shared.Next();
        public int Next(int maxValue) => System.Random.Shared.Next(maxValue);
    }
}
