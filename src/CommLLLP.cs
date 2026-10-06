// Copyright 2025 Robert Adams
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Microsoft.Extensions.Options;

using org.herbal3d.mblue.Common;
using org.herbal3d.mblue.Config;
using org.herbal3d.mblue.ecm;
using org.herbal3d.mblue.Logging;
using org.herbal3d.mblue.Statistics;

using LMV = LibreMetaverse;

namespace org.herbal3d.mblue.comm.os;

/// <summary>
/// Communication handler for Linden Lab Legacy Protocol
/// </summary>
public class CommLLLP : ICommProvider {
    private MBLogger<CommLLLP> m_log;

    private IOptions<CommOSConfig> m_CommOSConfig { get; set; }

    private CancellationToken m_cancellationToken;

    private CommLLLPStats m_stats = new CommLLLPStats();
    public StatisticCollection CommStatistics { get { return m_stats.CommStatistics; } }

    // ICommProvider.Name
    public string Name { get { return "CommLLLP"; } }

    private OSGridClient m_gridClient;
    private RegionCollection m_regionCollection;

    private MBVersions m_version;

    private ECMFactory m_ecmFactory;

    private Grids m_grids;

    // Mapping of simulators and region UUIDs to their corresponding RegionInfo objects.
    private LMV.DoubleDictionary<LMV.Simulator, LMV.UUID, RegionInfo> m_regionInfo = new LMV.DoubleDictionary<LMV.Simulator, LMV.UUID, RegionInfo>();

    // There are some messages that come in that are rare but could use some locking.
    // The main paths of prims and updates is pretty solid and multi-threaded but
    // others, like avatar control, can use a little locking.
    private Object m_opLock = new Object();

    public enum LoginStateCode {
        NotLoggedIn,
        ShouldLogIn,
        LogInFailed,
        LoggingIn,
        LoggedIn,
        ShouldLogOut,
        LoggingOut
    }
    public LoginStateCode m_loginState = LoginStateCode.NotLoggedIn;

    public bool IsConnected { get; protected set; } = false;

    public bool IsLoggedIn { get { return m_loginState == LoginStateCode.LoggedIn; } }

    /// <summary>
    /// Flag saying we're switching simulator connections. This would suppress things like teleport
    /// and certain status indications.
    /// </summary>
    public bool SwitchingSims { get { return m_SwitchingSims; } }
    protected bool m_SwitchingSims;       // true when we're setting up the connection to a different sim

    // The whole module is loaded or unloaded. This controls the whole trying to login loop.
    // m_shouldBeLoggedIn says whether we think we should be logged in. If true then the
    // first, last, ... parameters have the info to use logging in.
    // The logging in and out flags are true when we're doing that. Use to make sure
    // we don't try logging in or out again.
    // The module flag 'm_connected' is set true when logged in and connected.
    // protected bool m_shouldBeLoggedIn { get; set; } = false; // true if we should be logged in
    // protected LoginParams? m_loginParams { get; set; } // parameters to use when logging in
    // protected bool m_isLoggingIn { get; set; } = false;  // true if we are in the process of loggin in
    // protected bool m_isLoggingOut { get; set; } = false; // true if we are in the process of logging out

    // m_loginGrid has the displayable name. LoggedInGridName has cannoicalized name for app use.
    protected string m_loginGrid { get; set; } = "unknown";
    public string LoggedInGridName { get { return m_loginGrid.Replace(".", "_").ToLower(); } }
    protected string m_loginMsg { get; set; } = "";

    // If true, hold children objects until parent is available
    protected bool m_shouldHoldChildren = false;

    // There is one entity who is the main agent we control
    public IEntity? MainAgent { get; set; } = null;

    public CommLLLP(MBLogger<CommLLLP> pLog,
                    IOptions<CommOSConfig> pCommOSConfig,
                    OSGridClient pGridClient,
                    Grids pGrids,
                    ECMFactory pECMFactory,
                    RegionCollection pRegionCollection,
                    MBVersions pVersion
                    ) {
        m_log = pLog;
        m_CommOSConfig = pCommOSConfig;
        m_gridClient = pGridClient;
        m_grids = pGrids;
        m_ecmFactory = pECMFactory;
        m_regionCollection = pRegionCollection;
        m_version = pVersion;
        // m_waitTilLater = pQueueManager.CreateBasicWorkQueue("CommLLLP WaitTilLater");

        m_log.Log(MBLogLevel.DCOMM, "CommLLLP constructor completed");
    }

    public async Task StartAsync(CancellationToken cancellationToken) {
        m_log.Log(MBLogLevel.DCOMM, "CommLLLP StartAsync entered");
        m_cancellationToken = cancellationToken;

        InitConnectionFramework();

        while (!cancellationToken.IsCancellationRequested) {
            switch (m_loginState) {
                case LoginStateCode.NotLoggedIn:
                    // we are not logged in and are idle
                    break;
                case LoginStateCode.LoggingIn:
                    // we are in the process of logging in
                    break;
                case LoginStateCode.LogInFailed:
                    // we are in the process of logging in
                    break;
                case LoginStateCode.LoggedIn:
                    // we are logged in and active
                    break;
                case LoginStateCode.ShouldLogOut:
                    // Someone requested a logout
                    m_loginState = LoginStateCode.LoggingOut;
                    m_log.Log(MBLogLevel.DCOMM, "ShouldLogOut request. Logging out from LoggedIn state");
                    m_gridClient.GridClient.Network.Logout();
                    break;
                case LoginStateCode.LoggingOut:
                    break;
            }

            try {
                await Task.Delay(500, cancellationToken);
            } catch (TaskCanceledException) {
                m_log.Log(MBLogLevel.Information, "CommLLLP ExecuteAsync cancellation requested");
                // expected when we're shutting down
            }
        }
        DisconnectConnectionFramework();

        m_log.Log(MBLogLevel.DCOMM, "CommLLLP StartAsync exiting");
    }

    public Task StopAsync(CancellationToken cancellationToken) {
        m_log.Log(MBLogLevel.DCOMM, "CommLLLP StopAsync called");
        return Task.CompletedTask;
    }

    protected void InitConnectionFramework() {
        // Initialize the SL client
        try {
            LMV.IGridClient gc = m_gridClient.GridClient;

            // DEBUG DEBUG: try setting LMV log level to debug
            if (m_CommOSConfig.Value.EnableLowLevelCommDebugging) {
                LMV.Settings.LogLevel = Microsoft.Extensions.Logging.LogLevel.Debug;
                if (!LMV.Logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug)) {
                    m_log.Log(MBLogLevel.DCOMM, "LMV Logger not enabled");
                }
                LMV.Logger.Log("LMV Logger debug enabled from config", Microsoft.Extensions.Logging.LogLevel.Debug);
                m_log.Log(MBLogLevel.DCOMM, "LMV Logger debug enabled from config");
            }
            // END DEBUG DEBUG

            LMV.Settings.UserAgent = "LibreMetaverse";
            LMV.Settings.ResourceDir = "linden";
            LMV.Settings.BindAddress = System.Net.IPAddress.Any;
            LMV.Settings.MaxHttpConnections = 32;
            LMV.Settings.PacketArchiveSize = 1000;
            LMV.Settings.UdpReceiveQueueCapacity = 512;
            LMV.Settings.TexturePipelineRefreshInterval = 500.0f;
            LMV.Settings.SimulatorPoolTimeout = 2 * 60 * 1000;
            LMV.Settings.LogLevel = Microsoft.Extensions.Logging.LogLevel.Debug;

            gc.Settings.Connection.MfaEnabled = m_CommOSConfig.Value.Connection.MfaEnabled;
            gc.Settings.Connection.LoginServer = m_CommOSConfig.Value.Connection.LoginServer;
            gc.Settings.Timing.TransferTimeout = m_CommOSConfig.Value.Timing.TransferTimeout;
            gc.Settings.Timing.TeleportTimeout = m_CommOSConfig.Value.Timing.TeleportTimeout;
            gc.Settings.Timing.LogoutTimeout = m_CommOSConfig.Value.Timing.LogoutTimeout;
            gc.Settings.Timing.CapsTimeout = m_CommOSConfig.Value.Timing.CapsTimeout;
            gc.Settings.Timing.LoginTimeout = m_CommOSConfig.Value.Timing.LoginTimeout;
            gc.Settings.Timing.ResendTimeout = m_CommOSConfig.Value.Timing.ResendTimeout;
            gc.Settings.Timing.SimulatorTimeout = m_CommOSConfig.Value.Timing.SimulatorTimeout;
            gc.Settings.Timing.MapRequestTimeout = m_CommOSConfig.Value.Timing.MapRequestTimeout;
            gc.Settings.Timing.AgentUpdateInterval = m_CommOSConfig.Value.Timing.AgentUpdateInterval;
            gc.Settings.Timing.InterpolationInterval = m_CommOSConfig.Value.Timing.InterpolationInterval;
            gc.Settings.Packets.MaxPendingAcks = m_CommOSConfig.Value.Packets.MaxPendingAcks;
            gc.Settings.Packets.StatsQueueSize = m_CommOSConfig.Value.Packets.StatsQueueSize;
            gc.Settings.Packets.MaxResendCount = m_CommOSConfig.Value.Packets.MaxResendCount;
            gc.Settings.Packets.ThrottleOutgoing = m_CommOSConfig.Value.Packets.ThrottleOutgoing;
            gc.Settings.Packets.EnableSimStats = m_CommOSConfig.Value.Packets.EnableSimStats;
            gc.Settings.Packets.SendPings = m_CommOSConfig.Value.Packets.SendPings;
            gc.Settings.Packets.TrackUtilization = m_CommOSConfig.Value.Packets.TrackUtilization;
            gc.Settings.Agent.SendUpdates = m_CommOSConfig.Value.Agent.SendUpdates;
            gc.Settings.Agent.SendUpdatesRegularly = m_CommOSConfig.Value.Agent.SendUpdatesRegularly;
            gc.Settings.Agent.SendAppearance = m_CommOSConfig.Value.Agent.SendAppearance;
            gc.Settings.Agent.SendThrottle = m_CommOSConfig.Value.Agent.SendThrottle;
            gc.Settings.Agent.DisableUpdateDuplicateCheck = m_CommOSConfig.Value.Agent.DisableUpdateDuplicateCheck;
            gc.Settings.Agent.MultipleSims = m_CommOSConfig.Value.Agent.MultipleSims;
            gc.Settings.World.AlwaysDecodeObjects = m_CommOSConfig.Value.World.AlwaysDecodeObjects;
            gc.Settings.World.AlwaysRequestObjects = m_CommOSConfig.Value.World.AlwaysRequestObjects;
            gc.Settings.World.TrackObjects = m_CommOSConfig.Value.World.TrackObjects;
            gc.Settings.World.TrackAvatars = m_CommOSConfig.Value.World.TrackAvatars;
            gc.Settings.World.CachePrimitives = m_CommOSConfig.Value.World.CachePrimitives;
            gc.Settings.World.UseInterpolationTimer = m_CommOSConfig.Value.World.UseInterpolationTimer;
            gc.Settings.World.StoreLandPatches = m_CommOSConfig.Value.World.StoreLandPatches;
            gc.Settings.Parcel.TrackParcels = m_CommOSConfig.Value.Parcel.TrackParcels;
            gc.Settings.Parcel.AlwaysRequestAcl = m_CommOSConfig.Value.Parcel.AlwaysRequestAcl;
            gc.Settings.Parcel.AlwaysRequestDwell = m_CommOSConfig.Value.Parcel.AlwaysRequestDwell;
            gc.Settings.Parcel.PoolParcelData = m_CommOSConfig.Value.Parcel.PoolParcelData;
            gc.Settings.AssetCache.Enabled = m_CommOSConfig.Value.AssetCache.Enabled;
            gc.Settings.AssetCache.Dir = m_CommOSConfig.Value.AssetCache.Dir;
            gc.Settings.AssetCache.MaxSize = m_CommOSConfig.Value.AssetCache.MaxSize;
            gc.Settings.TexturePipeline.Enabled = m_CommOSConfig.Value.TexturePipeline.Enabled;
            gc.Settings.TexturePipeline.UseHttpTextures = m_CommOSConfig.Value.TexturePipeline.UseHttpTextures;
            gc.Settings.TexturePipeline.MaxConcurrentDownloads = m_CommOSConfig.Value.TexturePipeline.MaxConcurrentDownloads;
            gc.Settings.TexturePipeline.RequestTimeout = m_CommOSConfig.Value.TexturePipeline.RequestTimeout;
            gc.Settings.Logging.LogNames = m_CommOSConfig.Value.Logging.LogNames;
            gc.Settings.Logging.LogResends = m_CommOSConfig.Value.Logging.LogResends;
            gc.Settings.Logging.LogDiskCache = m_CommOSConfig.Value.Logging.LogDiskCache;

            gc.Self.Movement.AutoResetControls = false;
            gc.Self.Movement.UpdateInterval = m_CommOSConfig.Value.Movement.UpdateInterval;

            LMV.Settings.ResourceDir = m_CommOSConfig.Value.LMVResourceDir;

            /* From LookingGlass/KeeKee. Do we need to change throttle settings?
            // Crank up the throttle on texture downloads
            gc.Throttle.Total = 20000000.0f;
            gc.Throttle.Texture = 2446000.0f;
            gc.Throttle.Asset = 2446000.0f;
            gc.Settings.Packets.ThrottleOutgoing = false;
            */

            gc.Network.LoginProgress += Network_LoginProgress;
            gc.Network.Disconnected += Network_Disconnected;
            gc.Network.SimConnected += Network_SimConnected;
            gc.Network.EventQueueRunning += Network_EventQueueRunning;
            gc.Network.SimChanged += Network_SimChanged;
            gc.Network.EventQueueRunning += Network_EventQueueRunning;

            gc.Objects.ObjectPropertiesUpdated += Objects_ObjectPropertiesUpdated;
            gc.Objects.ObjectUpdate += Objects_ObjectUpdate;
            gc.Objects.ObjectDataBlockUpdate += Objects_ObjectDataBlockUpdate;
            gc.Objects.ObjectProperties += Objects_ObjectProperties;
            gc.Objects.TerseObjectUpdate += Objects_TerseObjectUpdate;
            gc.Objects.AvatarUpdate += Objects_AvatarUpdate;
            gc.Objects.KillObject += Objects_KillObject;
            gc.Avatars.AvatarAppearance += Avatars_AvatarAppearance;
            gc.Terrain.LandPatchReceived += Terrain_LandPatchReceived;

        } catch (Exception e) {
            m_log.Log(MBLogLevel.DBADERROR, "EXCEPTION BUILDING GRIDCLIENT: " + e.ToString());
        }

        // fake like this is the initial teleport
        m_SwitchingSims = true;
    }
    private void DisconnectConnectionFramework() {
        var gc = m_gridClient.GridClient;
        gc.Network.LoginProgress -= Network_LoginProgress;
        gc.Network.Disconnected -= Network_Disconnected;
        gc.Network.SimConnected -= Network_SimConnected;
        gc.Network.EventQueueRunning -= Network_EventQueueRunning;
        gc.Network.SimChanged -= Network_SimChanged;
        gc.Network.EventQueueRunning -= Network_EventQueueRunning;

        gc.Objects.ObjectPropertiesUpdated -= Objects_ObjectPropertiesUpdated;
        gc.Objects.ObjectUpdate -= Objects_ObjectUpdate;
        gc.Objects.ObjectDataBlockUpdate -= Objects_ObjectDataBlockUpdate;
        gc.Objects.ObjectProperties -= Objects_ObjectProperties;
        gc.Objects.TerseObjectUpdate -= Objects_TerseObjectUpdate;
        gc.Objects.AvatarUpdate -= Objects_AvatarUpdate;
        gc.Objects.KillObject -= Objects_KillObject;
        gc.Avatars.AvatarAppearance -= Avatars_AvatarAppearance;
        gc.Terrain.LandPatchReceived -= Terrain_LandPatchReceived;
    }

    /// <summary>
    /// Called by the REST handler to connect to a simulator.
    /// The login parameters are passed in which is the autorization info.
    /// Sets the state to "should be logged in" and processing should continue.
    /// </summary>
    /// <param name="pLoginParams"></param>
    /// <returns></returns>
    async Task<LoginResponse?> ICommProvider.StartLogin(LoginParams pLoginParams, CancellationToken cancellationToken) {
        // Are we already logged in?
        if (IsLoggedIn) {
            return null;
        }

        m_loginState = LoginStateCode.LoggingIn;
        LMV.LoginResponseData? llloginResponse = await DoLogin(pLoginParams, cancellationToken);

        // return the class that wraps the LL login response
        OSLoginResponse loginResponse = new OSLoginResponse(llloginResponse);

        return loginResponse;
    }

    // ICommProvider.StartLogout()
    async Task<bool> ICommProvider.StartLogout(CancellationToken cancellationToken) {
        m_log.Log(MBLogLevel.DCOMMDETAIL, "Disconnect request -- logout and disconnect");
        m_loginState = LoginStateCode.ShouldLogOut;
        return true;
    }

    // ICommProvider.StartTeleport()
    async Task<bool> ICommProvider.StartTeleport(string dest, CancellationToken cancellationToken) {
        bool ret = true;
        string sim = "";
        float x = 128;
        float y = 128;
        float z = 40;
        dest = dest.Trim();
        string[] tokens = dest.Split(new char[] { '/' });
        if (tokens.Length == 4) {
            sim = tokens[0];
            if (!float.TryParse(tokens[1], out x) ||
                            !float.TryParse(tokens[2], out y) ||
                            !float.TryParse(tokens[3], out z)) {
                m_log.Log(MBLogLevel.DBADERROR, "Could not parse teleport destination '{0}'", dest);
                ret = false;
            }
        } else if (tokens.Length == 1) {
            sim = tokens[0];
            x = 128;
            y = 128;
            z = 40;
        } else {
            m_log.Log(MBLogLevel.DBADERROR, "Did not recognize format of teleport destination: '{0}'", dest);
            ret = false;
        }
        if (ret && IsLoggedIn && (m_gridClient.GridClient != null)) {
            if (await m_gridClient.GridClient.Self.TeleportAsync(sim, new LMV.Vector3(x, y, z)).ConfigureAwait(false)) {
                m_log.Log(MBLogLevel.DBADERROR, "Teleport successful to '{0}'", dest);
                ret = true;
            } else {
                m_log.Log(MBLogLevel.DBADERROR, "Teleport to '{0}' failed", dest);
                ret = false;
            }
        }
        return ret;
    }

    public async Task<LMV.LoginResponseData> DoLogin(LoginParams pLoginParams, CancellationToken cancellationToken) {
        // Make a dummy response so the caller has something to work with
        LMV.LoginResponseData? errLoginResponse = new LMV.LoginResponseData() {
            Login = LMV.LoginState.False,
            Message = "Not logged in"
        };

        if (pLoginParams == null) {
            m_log.Log(MBLogLevel.DBADERROR, "StartLogin: no login parameters");
            errLoginResponse.Message = "No login parameters";
            return errLoginResponse;
        }
        m_log.Log(MBLogLevel.DCOMM, $"Starting login of {pLoginParams.FirstName} {pLoginParams.LastName}");
        LMV.LoginParams loginParams = m_gridClient.GridClient.Network.DefaultLoginParams(
                                            pLoginParams.FirstName ?? "Fred",
                                            pLoginParams.LastName ?? "Flintstone",
                                            pLoginParams.Password ?? "NoOneKnowsIt",
                                            m_version.AppName,
                                            m_version.AppVersion
        );

        string startLoc = pLoginParams.StartLocation ?? "";
        loginParams.Start = String.IsNullOrEmpty(startLoc) ? "last" : startLoc;

        string startGrid = pLoginParams.Grid ?? "";
        m_grids.SetCurrentGrid(String.IsNullOrEmpty(startGrid) ? "HippoGrid" : startGrid);

        var loginURI = m_grids.GridLoginURI(m_grids.CurrentGrid);

        if (String.IsNullOrEmpty(loginURI)) {
            m_log.Log(MBLogLevel.DBADERROR, "COULD NOT FIND URL OF GRID. Grid=" + m_grids.CurrentGrid);
            m_loginMsg = "Unknown Grid name";
            m_loginState = LoginStateCode.LogInFailed;
        } else {
            loginParams.URI = loginURI;
            // Update the Settings value incase someone uses it
            m_gridClient.GridClient.Settings.Connection.LoginServer = loginParams.URI;
            try {
                m_log.Log(MBLogLevel.DCOMM, "Logging in to grid {0} at {1} as {2} {3} start {4}",
                    m_grids.CurrentGrid,
                    loginParams.URI,
                    loginParams.FirstName,
                    loginParams.LastName,
                    loginParams.Start);
                LMV.LoginResponseData? response = await m_gridClient.GridClient.Network.LoginWithResponseAsync(loginParams, m_cancellationToken);
                if (response == null) {
                    m_log.Log(MBLogLevel.DBADERROR, "Login response is null");
                    m_loginState = LoginStateCode.LogInFailed;
                    errLoginResponse.Message = "Login response was null";
                    return errLoginResponse;
                } else {
                    if (response.Success) {
                        m_log.Log(MBLogLevel.DCOMM, "Login successful: {0}", response.Message);
                        // m_isConnected = true;
                        m_loginState = LoginStateCode.LoggedIn;
                        m_loginMsg = response.Message;
                        Comm_OnLoggedIn();
                    } else {
                        m_log.Log(MBLogLevel.DCOMM, "Login failed: {0}", response.Message);
                        m_loginState = LoginStateCode.LogInFailed;
                        m_loginMsg = response.Message;
                    }
                }
                return response;
            } catch (Exception e) {
                var errMsg = $@"BeginLogin exception: {e.Message ?? "No exception message"}";
                m_log.Log(MBLogLevel.DBADERROR, errMsg);
                m_loginState = LoginStateCode.LogInFailed;
                errLoginResponse.Message = errMsg;
                return errLoginResponse;
            }
        }
        return errLoginResponse;
    }

    // ===============================================================
    public virtual void Network_LoginProgress(object? sender, LMV.LoginProgressEventArgs args) {
        // m_log.Log(MBLogLevel.DCOMMDETAIL, "EVENT Network_LoginProgress: Login progress update");
        m_stats.NetLoginProgress.Event();
        m_log.Log(MBLogLevel.DCOMM, $"Login progress: status: {args.Status}, message: {args.Message}");
    }

    // ===============================================================
    public virtual void Network_Disconnected(object? sender, LMV.DisconnectedEventArgs args) {
        m_log.Log(MBLogLevel.DCOMMDETAIL, "EVENT Network_Disconnected: Disconnected from simulator");
        m_stats.NetDisconnected.Event();
        m_log.Log(MBLogLevel.DCOMM, "Disconnected");
        m_loginState = LoginStateCode.NotLoggedIn;
        IsConnected = false;
    }

    // ===============================================================
    public virtual void Network_SimConnected(object? sender, LMV.SimConnectedEventArgs args) {
        m_log.Log(MBLogLevel.DCOMMDETAIL, "EVENT Network_SimConnected: Simulator connected");
        m_stats.NetSimConnected.Event();
        if (!m_regionCollection.HasRegionInfo(args.Simulator)) {
            m_log.Log(MBLogLevel.DWORLD, $"Network_SimConnected: Simulator not in region collection. Adding region {args.Simulator.Name}");
            m_regionCollection.AddRegionInfo(new RegionInfo(args.Simulator));
        }
    }

    // ===============================================================
    public virtual void Network_EventQueueRunning(Object? sender, LMV.EventQueueRunningEventArgs args) {
        m_log.Log(MBLogLevel.DCOMMDETAIL, "EVENT Network_EventQueueRunning: Event queue running");

        // if we'd queued up actions, do them now that it's online
        DoAnyWaitingEvents(args.Simulator);

        // this is needed to make the avatar appear
        // TODO: figure out if the linking between agent and appearance is right
        // GridClient.Appearance.SetPreviousAppearance(true);
        LMV.IGridClient gc = m_gridClient.GridClient;
        gc.Appearance.RequestSetAppearance(true);
        gc.Self.Movement.UpdateFromHeading(0.0, true);
        if (gc.Network.CurrentSim != null) {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            gc.Parcels.RequestAllSimParcelsAsync(gc.Network.CurrentSim, false, new TimeSpan(0, 0, 30), m_cancellationToken).ConfigureAwait(false);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }
    }

    // ===============================================================
    public virtual void Network_SimChanged(object? sender, LMV.SimChangedEventArgs args) {
        m_log.Log(MBLogLevel.DCOMMDETAIL, "EVENT Network_SimChanged: Simulator changed");
        // disable teleports until we have a good connection to the simulator (event queue working)
        m_stats.NetSimChanged.Event();
        if (!m_gridClient.GridClient.Network.CurrentSim?.Caps?.IsEventQueueRunning ?? true) {
            m_SwitchingSims = true;
        }
        if (args.PreviousSimulator != null) {      // there is no prev sim the first time
            m_log.Log(MBLogLevel.DWORLD, "Simulator changed from {0}", args.PreviousSimulator.Name);
            // TODO: Handle the previous simulator being disconnected or cleaned up.
        }
    }

    // ===============================================================
    public virtual void Terrain_LandPatchReceived(object? sender, LMV.LandPatchReceivedEventArgs args) {
        m_log.Log(MBLogLevel.DCOMMDETAIL, "EVENT Terrain_LandPatchReceived: Land patch received");
        // m_log.Log(MBLogLevel.DWORLDDETAIL, "Land patch for {0}: {1}, {2}, {3}", 
        //             args.Simulator.Name, args.X, args.Y, args.PatchSize);
        if (m_regionCollection.TryGetRegionInfo(args.Simulator, out RegionInfo? ri)) {
            if (ri?.TerrainEntity is not null) {
                var terrainComponent = ri.TerrainEntity.Cmpt<CmptOSTerrain>();
                terrainComponent?.UpdatePatch(ri, args.X, args.Y, args.HeightMap);
                ri.TerrainEntity?.Update(new UpdateInfo(UpdateCodes.Terrain));
                return;
            }
        }
        m_log.Log(MBLogLevel.Warning, "No region or terrain for received land patch");
    }

    // ===============================================================
    public void Objects_ObjectDataBlockUpdate(object? sender, LMV.ObjectDataBlockUpdateEventArgs args) {
        m_log.Log(MBLogLevel.DCOMMDETAIL, "EVENT Objects_ObjectDataBlockUpdate: Object data block update received");
        return;
    }

    // ===============================================================
    public void Objects_ObjectUpdate(object? sender, LMV.PrimEventArgs args) {
        m_log.Log(MBLogLevel.DCOMMDETAIL, "EVENT Objects_ObjectUpdate: Object update received");
        RegionInfo? rit = m_regionCollection.GetRegionInfo(args.Simulator);

        QueueTilOnline<LMV.PrimEventArgs>(args.Simulator, sender, args, (sender, args, ri) => {
            if (args.IsAttachment) {
                Objects_AttachmentUpdate(sender, args);
                return;
            }

            m_stats.ObjObjectUpdate.Event();

            Task.Run(async () => {

                // Wait for the parent entity to exist if needed (children are relative to their parent)
                if (await WaitForParentIfNeeded(args.Prim.ParentID))
                    return;

                IEntity? updatedEntity;
                UpdateInfo updateFlags = new UpdateInfo();
                m_log.Log(MBLogLevel.DUPDATEDETAIL, "Object update: id={0}, p={1}, r={2}",
                    args.Prim.LocalID, args.Prim.Position.ToString(), args.Prim.Rotation.ToString());

                try {
                    updatedEntity = FindOrCreateEntity(args.Prim, ri, ref updateFlags);
                    if (updatedEntity is not null) {
                        // if there are animations for this entity
                        ProcessEntityAnimation(updatedEntity, ref updateFlags, args.Prim.AngularVelocity);
                        // send updates for this entity updates
                        ProcessEntityUpdates(updatedEntity, updateFlags);
                    }
                } catch (Exception e) {
                    m_log.Log(MBLogLevel.DBADERROR, "FAILED CREATION OF NEW PRIM: " + e.ToString());
                }
            });
        });

        return;
    }

    private IEntity FindOrCreateEntity(LMV.Primitive prim, RegionInfo pRegion, ref UpdateInfo pUpdateInfo) {
        if (!m_ecmFactory.TryGetEntity(prim.LocalID, out IEntity? entity)) {
            m_log.Log(MBLogLevel.DUPDATEDETAIL, "FindEntityOrCreateIfItDoesNotExist: creating new entity for local ID {0}", prim.LocalID);

            entity = m_ecmFactory.CreateEntity(new EntityName("prim" + prim.LocalID));
            entity.CreateAndAddComponent<CmptOSInfo>(pRegion, entity);
            entity.CreateAndAddComponent<CmptOSLocation>(entity);

            pUpdateInfo.AddFlag(UpdateCodes.New)
                        .AddFlag(UpdateCodes.Position)
                        .AddFlag(UpdateCodes.Rotation)
                        .AddFlag(UpdateCodes.Acceleration)
                        .AddFlag(UpdateCodes.AngularVelocity)
                        .AddFlag(UpdateCodes.Velocity);

            // new prim created
            // If this requires special rendering parameters add those parameters
            // At the moment, the only case is foliage
            /* TODO: is this needed. If needed, maybe move into CreateOSEntity
            if (args.Prim.PrimData.PCode == LMV.PCode.Grass
                        || args.Prim.PrimData.PCode == LMV.PCode.Tree
                        || args.Prim.PrimData.PCode == LMV.PCode.NewTree) {
                CmptOSSpecialRender srt = m_ECMFactory.CreateComponent<CmptOSSpecialRender>(updatedEntity, rcontext);
                srt.Type = SpecialRenderTypes.Foliage;
                srt.FoliageType = args.Prim.PrimData.PCode;
                srt.TreeType = args.Prim.TreeSpecies;
                updatedEntity.AddComponent<CmptOSSpecialRender>(srt);
            }
            */
        }
        return entity;
    }

    // Wait until the entity with the given parentID becomes available.
    // This does not guarantee that the entity will be available immediately; it will keep retrying until it is found.
    // Any timeout and error handling should be implemented by the caller.
    private async Task<IEntity> WaitForEntity(uint parentID) {
        // Wait for the entity with the given parentID to become available.
        IEntity? entity = null;
        while (m_ecmFactory.TryGetEntity(parentID, out entity) == false)
            await Task.Delay(100); // Wait and retry
        return entity ?? null!;
    }

    // return 'true' is the parent of this id exists in the world
    private bool ParentExists(uint parentID) {
        // if shouldn't be holding anything, fake like the parent is always here
        if (!m_shouldHoldChildren) return true;
        // if we don't need a parent no need to check
        if (parentID == 0) return true; // if no parent say we have the parent
                                        // see if the parent is known
        m_ecmFactory.TryGetEntity(parentID, out IEntity? parentEntity);
        return (parentEntity != null);
    }

    // If the prim has a parent, wait for the parent entity to exist before continuing.
    private async Task<bool> WaitForParentIfNeeded(uint pParentID) {
        bool failed = false;
        if (pParentID != 0 && !ParentExists(pParentID)) {
            m_log.Log(MBLogLevel.DUPDATEDETAIL, "Waiting for parent object: id={0}", pParentID);
            try {
                // We need to wait for the parent entity to exist before continuing
                //    since the location of the child object depends on it
                _ = await WaitForEntity(pParentID)
                        .WaitAsync(TimeSpan.FromMilliseconds(m_CommOSConfig.Value.ParentWaitTimeoutMS))
                        .ConfigureAwait(false);
            } catch (TimeoutException) {
                m_log.Log(MBLogLevel.DBADERROR, "Timeout waiting for parent object: id={0}", pParentID);
                // The prim will just not be displayed
                failed = true;
            }
        }
        return failed;
    }

    // For the moment, create only one animation for an entity and that is the angular rotation.
    private void ProcessEntityAnimation(IEntity? ent, ref UpdateInfo updateFlags, LMV.Vector3 angularVelocity) {
        try {
            // if  there is an angular velocity and this is not an avatar, pass the information
            // along as an animation (llTargetOmega)
            // we convert the information into a standard form
            if (angularVelocity != LMV.Vector3.Zero) {
                float rotPerSec = angularVelocity.Length() / Constants.TWOPI;
                LMV.Vector3 axis = LMV.Vector3.Normalize(angularVelocity);
                if (ent is not null) {
                    if (!ent.HasComponent<CmptOSAnimation>()) {
                        var newAnim = m_ecmFactory.CreateAndAddComponent<CmptOSAnimation>(ent);
                        m_log.Log(MBLogLevel.DUPDATEDETAIL, "Created prim animation on {0}", ent.Name);
                    }
                    CmptOSAnimation anim = ent.Cmpt<CmptOSAnimation>();
                    if (rotPerSec != anim.StaticRotationRotPerSec || axis != anim.StaticRotationAxis) {
                        anim.AngularVelocity = angularVelocity;   // legacy. Remove when other part plumbed
                        anim.StaticRotationAxis = axis;
                        anim.StaticRotationRotPerSec = rotPerSec;
                        anim.DoStaticRotation = true;
                        updateFlags.AddFlag(UpdateCodes.Animation);
                        m_log.Log(MBLogLevel.DUPDATEDETAIL, "Updating prim animation on {0}", ent.Name);
                    }
                }
            }
        } catch (Exception e) {
            m_log.Log(MBLogLevel.DBADERROR, "FAILED ProcessEntityAnimation: " + e.ToString());
        }
    }

    // Entity has been updated. Tell the world about the updates.
    private void ProcessEntityUpdates(IEntity? ent, UpdateInfo updateFlags) {
        try {
            if (ent is not null) {
                // special update for the agent so it knows there is new info from the network
                // The real logic to push the update through happens in the IEntityAvatar.Update()
                if (ent == this.MainAgent) {
                    // TODO: figure out if we need to do anything special for the main agent
                    // ent.DataUpdate(updateFlags);
                }
                // Tell the world the entity is updated
                ent.Update(updateFlags);
            }
        } catch (Exception e) {
            m_log.Log(MBLogLevel.DBADERROR, "FAILED ProcessEntityUpdates: " + e.ToString());
        }
    }
    // ===============================================================
    // The packet library has updated the attachement points in the prim already
    // This needs to get the attachment loaded into the world
    public void Objects_AttachmentUpdate(object? sender, LMV.PrimEventArgs args) {
        RegionInfo? rit = m_regionCollection.GetRegionInfo(args.Simulator);

        QueueTilOnline<LMV.PrimEventArgs>(args.Simulator, sender, args, (sender, args, ri) => {
            m_log.Log(MBLogLevel.DCOMMDETAIL, "EVENT Objects_AttachmentUpdate: Attachment update received");
            m_stats.ObjAttachmentUpdate.Event();

            Task.Run(async () => {

                // Wait for the parent entity to exist if needed (children are relative to their parent)
                if (await WaitForParentIfNeeded(args.Prim.ParentID))
                    return;

                m_log.Log(MBLogLevel.DUPDATEDETAIL, "OnNewAttachment: id={0}, lid={1}", args.Prim.ID.ToString(), args.Prim.LocalID);

                try {
                    // if new or not, assume everything about this entity has changed
                    UpdateInfo updateFlags = new UpdateInfo();
                    IEntity? ent = FindOrCreateEntity(args.Prim, ri, ref updateFlags);

                    if (ent is not null & updateFlags.HasFlag(UpdateCodes.New)) {
                        m_log.Log(MBLogLevel.DUPDATEDETAIL, "OnNewAttachment: entity is new");

                        // Find the index of the attachment within the prim's name-value pairs
                        string? attachmentID = "1"; // default attachment ID
                        if (args.Prim.NameValues != null) {
                            foreach (LMV.NameValue nv in args.Prim.NameValues) {
                                m_log.Log(MBLogLevel.DCOMMDETAIL, $"AttachmentUpdate: ent={ent?.Name}, {nv.Name}->{nv.Value}");
                                if (nv.Name == "AttachItemID") {
                                    attachmentID = nv.Value?.ToString() ?? "1";
                                    break;
                                }
                            }
                        }
                        _ = ent?.CreateAndAddComponent<CmptOSAttachment>(attachmentID, args.Prim.PrimData.AttachmentPoint);
                    } else {
                        m_log.Log(MBLogLevel.DBADERROR, "FAILED CREATION OF NEW ATTACHMENT");
                    }
                    ent?.Update(updateFlags);
                } catch (Exception e) {
                    m_log.Log(MBLogLevel.DBADERROR, "FAILED CREATION OF NEW ATTACHMENT: " + e.ToString());
                }
            });
        });
        return;
    }
    // ===============================================================
    private void Objects_TerseObjectUpdate(object? sender, LMV.TerseObjectUpdateEventArgs args) {
        m_log.Log(MBLogLevel.DCOMMDETAIL, "EVENT Objects_TerseObjectUpdate: Terse object update received");
        RegionInfo? rit = m_regionCollection.GetRegionInfo(args.Simulator);

        QueueTilOnline<LMV.TerseObjectUpdateEventArgs>(args.Simulator, sender, args, (sender, args, ri) => {
            if (args.Simulator == null) {
                m_log.Log(MBLogLevel.DBADERROR, "TerseObjectUpdate: Simulator is null");
                return;
            }

            if (args.Prim.ID == LMV.UUID.Zero) {
                m_log.Log(MBLogLevel.DBADERROR, "TerseObjectUpdate: received prim with UUID zero");
                return;
            }

            m_stats.ObjTerseUpdate.Event();

            Task.Run(async () => {

                if (await WaitForParentIfNeeded(args.Prim.ParentID))
                    return;

                IEntity? updatedEntity = null;
                UpdateInfo updateFlags = new UpdateInfo();

                LMV.ObjectMovementUpdate update = args.Update;
                var prim = args.Prim;
                if (prim.Acceleration != update.Acceleration) updateFlags.AddFlag(UpdateCodes.Acceleration);
                if (prim.Velocity != update.Velocity) updateFlags.AddFlag(UpdateCodes.Velocity);
                if (prim.AngularVelocity != update.AngularVelocity) updateFlags.AddFlag(UpdateCodes.AngularVelocity);
                if (prim.Position != update.Position) updateFlags.AddFlag(UpdateCodes.Position);
                if (prim.Rotation != update.Rotation) updateFlags.AddFlag(UpdateCodes.Rotation);
                if (update.Avatar) updateFlags.AddFlag(UpdateCodes.CollisionPlane);
                if (update.Textures != null) updateFlags.AddFlag(UpdateCodes.Textures);
                m_log.Log(MBLogLevel.DUPDATEDETAIL, "Object update: id={0}, p={1}, r={2}, what={3}",
                        update.LocalID, update.Position.ToString(), update.Rotation.ToString(),
                        updateFlags.ToString());

                try {
                    updatedEntity = FindOrCreateEntity(args.Prim, ri, ref updateFlags);
                    // if there are animations for this entity
                    ProcessEntityAnimation(updatedEntity, ref updateFlags, args.Prim.AngularVelocity);
                    // send updates for this entity updates
                    ProcessEntityUpdates(updatedEntity, updateFlags);
                } catch (Exception e) {
                    m_log.Log(MBLogLevel.DBADERROR, "FAILED CREATION OF NEW PRIM: " + e.ToString());
                }
            });
        });

        return;
    }
    // ===============================================================
    private void Objects_ObjectProperties(object? sender, LMV.ObjectPropertiesEventArgs args) {
        m_log.Log(MBLogLevel.DUPDATEDETAIL, "EVENT Objects_ObjectProperties:");
        m_stats.ObjObjectProperties.Event();
    }
    // ===============================================================
    private void Objects_ObjectPropertiesUpdated(object? sender, LMV.ObjectPropertiesUpdatedEventArgs args) {
        m_log.Log(MBLogLevel.DCOMMDETAIL, "EVENT Objects_ObjectPropertiesUpdated: Object properties updated received");
        m_stats.ObjObjectPropertiesUpdate.Event();
    }
    // ===============================================================
    public void Objects_AvatarUpdate(object? sender, LMV.AvatarUpdateEventArgs args) {
        m_log.Log(MBLogLevel.DCOMMDETAIL, "EVENT Objects_AvatarUpdate: Avatar update received");

        RegionInfo? rit = FindRegion(args.Simulator);

        QueueTilOnline<LMV.AvatarUpdateEventArgs>(args.Simulator, sender, args, (sender, args, ri) => {
            Task.Run(async () => {

                if (await WaitForParentIfNeeded(args.Avatar.ParentID))
                    return;

                m_stats.ObjAvatarUpdate.Event();
                m_log.Log(MBLogLevel.DUPDATEDETAIL, "Objects_AvatarUpdate: cntl={0}, parent={1}, p={2}, r={3}",
                            args.Avatar.ControlFlags.ToString("x"), args.Avatar.ParentID,
                            args.Avatar.Position, args.Avatar.Rotation);
                UpdateInfo updateFlags = new UpdateInfo();

                // This is an avatar, assume somethings changed no matter what
                updateFlags.AddFlag(UpdateCodes.CollisionPlane);

                EntityName avatarEntityName = new EntityName($"Avatar{args.Avatar.ID}");

                IEntity? updatedEntity = FindOrCreateEntity(args.Avatar, ri, ref updateFlags);

                if (updatedEntity != null) {
                    updatedEntity.Cmpt<ICmptLocation>().LocalPosition = org.herbal3d.mblue.Common.StructuredData.Vector3.FromObject(args.Avatar.Position);
                    updatedEntity.Cmpt<ICmptLocation>().Heading = org.herbal3d.mblue.Common.StructuredData.Quaternion.FromObject(args.Avatar.Rotation);
                    // We check here if this avatar goes with the agent in the world
                    // If this av is with the agent, make the connection
                    m_log.Log(MBLogLevel.DUPDATEDETAIL, "AvatarUpdate: Alid={0}, Clid={1}",
                                            args.Avatar.LocalID, m_gridClient.GridClient.Self.LocalID);
                    if (args.Avatar.LocalID == m_gridClient.GridClient.Self.LocalID) {
                        m_log.Log(MBLogLevel.DUPDATEDETAIL, "AvatarUpdate: associating agent with new avatar");
                        this.MainAgent = updatedEntity;
                    }
                    // send updates for the updated entity
                    ProcessEntityUpdates(updatedEntity, updateFlags);
                }
            });
        });
        return;
    }

    // ===============================================================
    public virtual void Objects_KillObject(object? sender, LMV.KillObjectEventArgs args) {
        m_log.Log(MBLogLevel.DCOMMDETAIL, "EVENT Objects_KillObject: Object kill received");
        RegionInfo? rit = FindRegion(args.Simulator);

        QueueTilOnline<LMV.KillObjectEventArgs>(args.Simulator, sender, args, (sender, args, ri) => {
            m_stats.ObjKillObject.Event();
            m_log.Log(MBLogLevel.DWORLDDETAIL, "Object killed:");

            try {
                IEntity removedEntity;
                if (m_ecmFactory.TryGetEntity(args.ObjectLocalID, out removedEntity)) {
                    m_ecmFactory.ReleaseEntity(removedEntity);
                }
            } catch (Exception e) {
                m_log.Log(MBLogLevel.DBADERROR, "FAILED DELETION OF OBJECT: " + e.ToString());
            }
        });
        return;
    }

    // ===============================================================
    public virtual void Avatars_AvatarAppearance(object? sender, LMV.AvatarAppearanceEventArgs args) {
        m_log.Log(MBLogLevel.DCOMMDETAIL, "EVENT Avatars_AvatarAppearance: Avatar appearance received");
        RegionInfo? rit = FindRegion(args.Simulator);

        QueueTilOnline<LMV.AvatarAppearanceEventArgs>(args.Simulator, sender, args, (sender, args, ri) => {
            m_log.Log(MBLogLevel.DCOMMDETAIL, "AvatarAppearance: id={0}", args.AvatarID.ToString());

            // the appearance information is stored in the avatar info in libomv
            // We just kick the system to look at it
            lock (m_opLock) {
                EntityName avatarEntityName = new EntityName($"Avatar{args.AvatarID}");
                IEntity? ent;
                if (m_ecmFactory.TryGetEntity(avatarEntityName, out ent)) {
                    ent?.Update(new UpdateInfo(UpdateCodes.Appearance));
                }
            }
        });
        return;
    }
    // ===============================================================
    /// <summary>
    /// Called when we just log in. We create our agent and put it into the world
    /// </summary>
    public virtual void Comm_OnLoggedIn() {
        m_log.Log(MBLogLevel.DWORLD, "Comm_OnLoggedIn:");
        // I work by taking LLLP messages and updating the agent
        // The agent will be updated in the world (usually by the viewer)
        // Create the two way communication linkage
        // this.MainAgent.OnUpdated += new AgentUpdatedCallback(Comm_OnAgentUpdated);
    }

    // ===============================================================
    public virtual void Comm_OnLoggedOut() {
        m_log.Log(MBLogLevel.DWORLD, "Comm_OnLoggedOut:");
    }

    // ===============================================================
    public virtual void Comm_OnAgentUpdated(IEntity agnt, UpdateCodes what) {
        m_log.Log(MBLogLevel.DWORLDDETAIL, "Comm_OnAgentUpdated:");

    }

    // ===============================================================
    // given a simulator. Find the region info that we store the stuff in
    // Note that, if we are not connected, we just return null thus showing our unhappiness.
    public virtual RegionInfo? FindRegion(LMV.Simulator? sim) {
        if (sim == null) return null;
        RegionInfo? foundRegion = null;
        if (IsConnected) {
            m_regionCollection.TryGetRegionInfo(sim.ID, out foundRegion);
        }
        return foundRegion;
    }

    // There are object events that are received before the simulator is fully online.
    // These events need to be queued until the simulator is ready.
    delegate void QueueTilOnlineDelegate<T>(object? pSender, T pArgs, RegionInfo pRi) where T : EventArgs;
    private List<SavedEvent> m_waitTilOnline = new List<SavedEvent>();

    private struct SavedEvent {
        public RegionInfo? Region;
        public LMV.Simulator? Simulator;
        public object? Sender;
        public EventArgs Args;
        public Type ArgsType;
        public Delegate Callback;
    }

    // We receive object events before the simulator is fully online. This method queues them until the simulator is ready.
    private void QueueTilOnline<T>(LMV.Simulator? pSim, object? pSender, T pArgs, QueueTilOnlineDelegate<T> pCallback) where T : EventArgs {
        if (pSim is not null) {
            if (pSim.IsEventQueueRunning() && pSim.IsRunning) {
                pCallback(pSender, pArgs, FindRegion(pSim));
                return;
            }
        }
        // Above tests failed so this event needs to be queued until the simulator is online.
        m_log.Log(MBLogLevel.DCOMMDETAIL, $"Queueing waiting event of type {typeof(T).Name} for simulator {pSim?.Name ?? "unknown"}");
        // DEBUG DEBUG
        if (pSim is null) {
            m_log.Log(MBLogLevel.DCOMMDETAIL, $"Queueing because pSim is null");
        } else {
            if (pSim.IsEventQueueRunning() is false)
                m_log.Log(MBLogLevel.DCOMMDETAIL, $"Queueing because simulator event queue is not running");
            if (pSim.IsRunning is false)
                m_log.Log(MBLogLevel.DCOMMDETAIL, $"Queueing because simulator is not running");
        }
        // end DEBUG DEBUG
        lock (m_waitTilOnline) {
            m_waitTilOnline.Add(new SavedEvent {
                Region = null,
                Simulator = pSim,
                Sender = pSender,
                Args = pArgs,
                Callback = pCallback,
                ArgsType = typeof(T)
            });
        }
        return;
    }

    // Function to process any events that were waiting for the simulator to come online.
    // See the implementation of QueueTilOnline for how events are queued until the simulator is online.
    private void DoAnyWaitingEvents(LMV.Simulator pSimulator) {
        m_log.Log(MBLogLevel.DCOMMDETAIL, $"Processing any waiting events for simulator {pSimulator.Name}");
        // Iterate through the list of saved events and process those that belong to the given simulator.
        var eventsToProcess = new List<SavedEvent>();
        lock (m_waitTilOnline) {
            eventsToProcess = m_waitTilOnline.Where(e => e.Simulator == pSimulator).ToList();
            m_waitTilOnline.RemoveAll(e => e.Simulator == pSimulator);
        }
        m_log.Log(MBLogLevel.DCOMMDETAIL, $"Found {eventsToProcess.Count} waiting events for simulator {pSimulator.Name} in queue of {m_waitTilOnline.Count} waiing event");
        foreach (SavedEvent savedEvent in eventsToProcess) {
            if (savedEvent.Region?.Simulator == pSimulator) {
                // If the region info was not present when the event was queued, try to find it using the simulator reference.
                RegionInfo? regionInfo = savedEvent.Region ?? FindRegion(pSimulator);
                if (regionInfo is null) {
                    m_log.Log(MBLogLevel.DBADERROR, $"DoAnyWaitingEvents: No region context for simulator {pSimulator.Name} for event of type {savedEvent.ArgsType.Name}");
                    continue;
                }
                m_log.Log(MBLogLevel.DCOMMDETAIL, $"Processing waiting event of type {savedEvent.ArgsType.Name} in region {regionInfo.Name}");
                Task.Run(() => savedEvent.Callback.DynamicInvoke(savedEvent.Sender, savedEvent.Args, regionInfo));
                // savedEvent.Callback.DynamicInvoke(savedEvent.Sender, savedEvent.Args, regionInfo);
            }
        }
        return;
    }
}
