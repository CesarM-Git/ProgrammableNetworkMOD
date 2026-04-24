using Mafi;
using Mafi.Unity;
using System.Collections.Generic;
using System.Linq;
using Mafi.Core.Entities;
using Mafi.Core;
using Mafi.Core.GameLoop;

namespace ProgramableNetwork.Data.Antene
{
    /// <summary>
    /// Manages AM signal propagation between antennas. Similar to FMManager but:
    /// - Signal lookup uses a receiving antenna's position (not the controller's position)
    /// - AM broadcast range is 10000 tiles (5x FM's 2000), multiplied by antenna DistanceBoost
    /// - Picks the closest broadcasting antenna with an active signal on the requested channel
    /// </summary>
    [GlobalDependency(RegistrationMode.AsEverything, false, false)]
    public class AMManager : IDataBandManager
    {
        /// <summary>
        /// AM antenna-to-antenna broadcast range in tiles. 5x FM's 2000-tile proto distance.
        /// </summary>
        public static readonly Fix32 AM_BROADCAST_RANGE = 20000.ToFix32();

        private readonly Dictionary<Tile3i, Antena> m_antenas;

        public AMManager(IEntitiesManager entitiesManager, IGameLoopEvents gameLoopEvents)
        {
            m_antenas = entitiesManager.GetAllEntitiesOfType<Antena>().ToDictionary(a => a.Position3f.Tile3i);

            entitiesManager.EntityAdded.AddNonSaveable(this, OnAdded);
            entitiesManager.EntityRemoved.AddNonSaveable(this, OnRemoved);
        }

        /// <summary>
        /// Query the AM signal at a specific channel index, as received by the given antenna.
        /// Scans all other AM-configured antennas in range, picks the closest one with a valid signal.
        /// </summary>
        /// <param name="receiverAntena">The receiving antenna (used for position and range calculation)</param>
        /// <param name="channelIdx">The AM channel index to read</param>
        /// <returns>Signal value from the closest broadcasting antenna, or Fix32.Zero if none found</returns>
        public Fix32 Signal(Antena receiverAntena, int channelIdx)
        {
            if (receiverAntena == null)
                return Fix32.Zero;

            Tile3i receiverPosition = receiverAntena.Position3f.Tile3i;
            Fix32 bestDistance = Fix32.MaxValue;
            Fix32 bestValue = Fix32.Zero;

            foreach ((Tile3i tile, Antena broadcaster) in m_antenas)
            {
                // Skip self
                if (broadcaster == receiverAntena)
                    continue;

                // Only consider AM-configured antennas
                if (broadcaster.DataBand is not AMDataBand dataBand)
                    continue;

                // Skip disabled or paused antennas
                if (broadcaster.IsNotEnabled || broadcaster.IsPaused)
                    continue;

                // Check if the channel has a valid (non-expired) signal
                if (channelIdx < 0 || channelIdx >= dataBand.ActiveChannels.Count)
                    continue;

                AMDataBandChannel channel = dataBand.ActiveChannels[channelIdx];
                if (channel.ValidIterations < 1)
                    continue;

                // Distance check: use the broadcasting antenna's range
                Fix32 distance = (tile.ToCenterVector3() - receiverPosition.ToCenterVector3()).magnitude.ToFix32();
                Fix32 maxDistance = AM_BROADCAST_RANGE * broadcaster.Prototype.DistanceBoost;

                if (distance > maxDistance)
                    continue;

                // Pick the closest broadcaster
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestValue = channel.Value ?? Fix32.Zero;
                }
            }

            return bestValue;
        }

        /// <summary>
        /// Returns all active AM signals visible from the given antenna's position.
        /// For each channel index with an active signal, returns the closest broadcaster's
        /// signal strength and channel data.
        /// </summary>
        public Dictionary<int, (Fix32 signalStrength, AMDataBandChannel channelInfo, Antena source)> Signals(Antena receiverAntena)
        {
            var distances = new Dictionary<int, Fix32>();
            var channels = new Dictionary<int, (Fix32, AMDataBandChannel, Antena)>();

            if (receiverAntena == null)
                return channels;

            Tile3i receiverPosition = receiverAntena.Position3f.Tile3i;

            foreach ((Tile3i tile, Antena broadcaster) in m_antenas)
            {
                if (broadcaster == receiverAntena)
                    continue;

                if (broadcaster.DataBand is not AMDataBand dataBand)
                    continue;

                if (broadcaster.IsNotEnabled || broadcaster.IsPaused)
                    continue;

                Fix32 distance = (tile.ToCenterVector3() - receiverPosition.ToCenterVector3()).magnitude.ToFix32();
                Fix32 maxDistance = AM_BROADCAST_RANGE * broadcaster.Prototype.DistanceBoost;

                if (distance > maxDistance)
                    continue;

                Fix32 strength = Fix32.One - (distance / maxDistance);

                foreach (AMDataBandChannel channel in dataBand.ActiveChannels)
                {
                    if (channel.ValidIterations < 1)
                        continue;

                    if (distances.TryGetValue(channel.Index, out Fix32 existing) && existing <= distance)
                        continue;

                    distances[channel.Index] = distance;
                    channels[channel.Index] = (strength, channel, broadcaster);
                }
            }

            return channels;
        }

        private void OnAdded(IEntity entity)
        {
            if (entity is Antena antena)
            {
                m_antenas[antena.Position3f.Tile3i] = antena;
            }
        }

        private void OnRemoved(IEntity entity)
        {
            if (entity is Antena antena)
            {
                m_antenas.Remove(antena.Position3f.Tile3i);
            }
        }
    }
}
