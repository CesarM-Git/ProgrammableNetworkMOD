using Mafi;
using System.Collections.Generic;

namespace ProgramableNetwork.Data.Antene
{
    /// <summary>
    /// Provides AM signal data for the antenna inspector UI.
    /// Returns the antenna's own active channels (from WorldMapMine redirects
    /// and local broadcaster modules).
    /// </summary>
    [GlobalDependency(RegistrationMode.AsEverything, false, false)]
    public class AMManager
    {
        /// <summary>
        /// Returns all active AM signals on the given antenna's local channels.
        /// Used by the "Received signals" UI panel in the antenna inspector.
        /// </summary>
        public Dictionary<int, (Fix32 signalStrength, AMDataBandChannel channelInfo, Antena source)> Signals(Antena antenna)
        {
            var channels = new Dictionary<int, (Fix32, AMDataBandChannel, Antena)>();

            if (antenna == null)
                return channels;

            if (antenna.DataBand is AMDataBand dataBand)
            {
                foreach (AMDataBandChannel channel in dataBand.ActiveChannels)
                {
                    if (channel.ValidIterations < 1)
                        continue;

                    channels[channel.Index] = (Fix32.One, channel, antenna);
                }
            }

            return channels;
        }
    }
}
