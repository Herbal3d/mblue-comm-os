// Copyright 2026 Robert Adams
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace org.herbal3d.mblue.Config {

    public class CommOSConfig {
        public static string subSectionName { get; set; } = "CommOS";

        // Whether Comm should hold objects if the parent doesn't exist
        public bool ShouldHoldChildren { get; set; } = true;
        // Wether to connect to multiple sims
        // Milliseconds between movement messages sent to server
        public int MovementUpdateInterval { get; set; } = 100;

        // Enable debug messages from the communication layer (usually libremetaverse))
        public bool EnableLowLevelCommDebugging { get; set; } = false;

        // libreMetaverse operation requires a bunch of resources
        public string LMVResourceDir { get; set; } = "../assets/openmetaverse_data";
        // files used if a required texture is not found
        public string NoTextureFilename { get; set; } = "../assets/NoTexture.png";
        public string NoSculptyFilename { get; set; } = "../assets/NoSculpty.png";

        public ConnectionSettings Connection { get; set; } = new ConnectionSettings();
        public TimingSettings Timing { get; set; } = new TimingSettings();
        public PacketSettings Packets { get; set; } = new PacketSettings();
        public AgentSettings Agent { get; set; } = new AgentSettings();
        public WorldSettings World { get; set; } = new WorldSettings();
        public ParcelSettings Parcel { get; set; } = new ParcelSettings();
        public AssetCacheSettings AssetCache { get; set; } = new AssetCacheSettings();
        public TexturePipelineSettings TexturePipeline { get; set; } = new TexturePipelineSettings();
        public LoggingSettings Logging { get; set; } = new LoggingSettings();
        public MovementSettings Movement { get; set; } = new MovementSettings();
    }

    public struct ConnectionSettings {
        public ConnectionSettings() { }
        public bool MfaEnabled { get; set; } = false;
        public string LoginServer { get; set; } = "";
    }
    public struct TimingSettings {
        public TimingSettings() { }
        public int TransferTimeout { get; set; } = 90_000;
        public int TeleportTimeout { get; set; } = 40_000;
        public int LogoutTimeout { get; set; } = 5_000;
        public int CapsTimeout { get; set; } = 60_000;
        public int LoginTimeout { get; set; } = 60_000;
        public int ResendTimeout { get; set; } = 4_000;
        public int SimulatorTimeout { get; set; } = 30_000;
        public int MapRequestTimeout { get; set; } = 5_000;
        public int AgentUpdateInterval { get; set; } = 500;
        public int InterpolationInterval { get; set; } = 250;
    }
    public struct PacketSettings {
        public PacketSettings() { }
        public int MaxPendingAcks { get; set; } = 10;
        public int StatsQueueSize { get; set; } = 5;
        public int MaxResendCount { get; set; } = 3;
        public bool ThrottleOutgoing { get; set; } = true;
        public bool EnableSimStats { get; set; } = true;
        public bool SendPings { get; set; } = true;
        public bool TrackUtilization { get; set; } = false;
    }
    public struct AgentSettings {
        public AgentSettings() { }
        public bool SendUpdates { get; set; } = true;
        public bool SendUpdatesRegularly { get; set; } = true;
        public bool SendAppearance { get; set; } = true;
        public bool SendThrottle { get; set; } = true;
        public bool DisableUpdateDuplicateCheck { get; set; } = true;
        public bool MultipleSims { get; set; } = false;
    }
    public struct WorldSettings {
        public WorldSettings() { }
        public bool AlwaysDecodeObjects { get; set; } = true;
        public bool AlwaysRequestObjects { get; set; } = true;
        public bool TrackObjects { get; set; } = true;
        public bool TrackAvatars { get; set; } = true;
        public bool CachePrimitives { get; set; } = false;
        public bool UseInterpolationTimer { get; set; } = false; // library default is true
        public bool StoreLandPatches { get; set; } = true;  // library default is false
    }
    public struct ParcelSettings {
        public ParcelSettings() { }
        public bool TrackParcels { get; set; } = true;
        public bool AlwaysRequestAcl { get; set; } = false;
        public bool AlwaysRequestDwell { get; set; } = false;
        public bool PoolParcelData { get; set; } = false;
    }
    public struct AssetCacheSettings {
        public AssetCacheSettings() { }
        public bool Enabled { get; set; } = true;
        public string Dir { get; set; } = "../cache";
        public long MaxSize { get; set; } = 1_000_000_000;
    }
    public struct TexturePipelineSettings {
        public TexturePipelineSettings() { }
        public bool Enabled { get; set; } = true;
        public bool UseHttpTextures { get; set; } = true;
        public int MaxConcurrentDownloads { get; set; } = 4;
        public int RequestTimeout { get; set; } = 120_000;
    }
    public struct LoggingSettings {
        public LoggingSettings() { }
        public bool LogNames { get; set; } = true;
        public bool LogResends { get; set; } = true;
        public bool LogDiskCache { get; set; } = true;
    }
    public struct MovementSettings {
        public MovementSettings() { }
        public int UpdateInterval { get; set; } = 100;
    }
}
