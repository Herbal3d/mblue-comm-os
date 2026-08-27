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

using LMV = LibreMetaverse;
using LMVSD = LibreMetaverse.StructuredData;

using org.herbal3d.mblue.Common;
using org.herbal3d.mblue.Logging;
using org.herbal3d.mblue.Config;
using org.herbal3d.mblue.Statistics;


namespace org.herbal3d.mblue.comm {
    /// <summary>
    /// Linkage between asset requests and the underlying asset server.
    /// This uses the OpenMetaverse connection to the server to load the
    /// asset (texture) into the filesystem.
    /// </summary>
    public sealed class LLAssetContext : IDumpable {

        private MBLogger<LLAssetContext> m_log;
        private IOptions<CommOSConfig> m_CommOSConfig;

        private SemaphoreSlim m_textureThrottle;
        private StatNumber m_queuedTextureRequests = new StatNumber("QueuedTextureRequests", "Number of texture requests waiting to be sent");
        private StatNumber m_activeTextureRequests = new StatNumber("ActiveTextureRequests", "Number of active texture requests");

        private OMV.GridClient m_gridClient;

        private StatisticCollection m_stats;

        public LLAssetContext(MBLogger<LLAssetContext> pLog,
                            IOptions<CommOSConfig> pCommOSConfig) {

            // This MAX is only used for the UDP texture requests (not HTTP)
            m_gridClient = pGridClient.GridClient;

            m_log = pLog;
            m_CommOSConfig = pCommOSConfig;

            m_gridClient.Settings.MAX_CONCURRENT_TEXTURE_DOWNLOADS = m_maxRequests;
            m_gridClient.Settings.USE_ASSET_CACHE = true;
            m_gridClient.Settings.ASSET_CACHE_DIR = CacheDirBase;
            m_gridClient.Assets.Cache.ComputeAssetCacheFilename = ComputeTextureFilename;

            m_maxRequests = pAssetConfig.Value.MaxTextureRequests;

            m_textureThrottle = new(m_maxRequests, m_maxRequests);

            m_stats = new StatisticCollection("LLAssetContext"); ;
            m_stats.AddStat(m_activeTextureRequests);
            m_stats.AddStat(m_queuedTextureRequests);
        }

        // Return the cache filename for the given texture ID
        // Called from inside the libomv asset cache system
        public string ComputeTextureFilename(string cacheDir, OMV.UUID textureID) {
            EntityNameLL entName = EntityNameLL.ConvertTextureWorldIDToEntityName(this, textureID);
            string textureFilename = Path.Combine(CacheDirBase, entName.CacheFilename);
            // m_log.Log(LogLevel.DTEXTUREDETAIL, "ComputeTextureFilename: " + textureFilename);

            // make sure the recieving directory is there for the texture
            MakeParentDirectoriesExist(textureFilename);

            // m_log.Log(LogLevel.DTEXTUREDETAIL, "ComputerTextureFilename: returning " + textureFilename);
            return textureFilename;
        }

        /// <summary>
        /// based only on the name of the texture entity, decide if it's mine.
        /// Here we check for the name of our asset context at the beginning of the
        /// name (where the host part is)
        /// </summary>
        /// <param name="textureEntityName"></param>
        /// <returns></returns>
        public override bool isTextureOwner(EntityName textureEntityName) {
            return textureEntityName.Name.StartsWith(Name + EntityName.PartSeparator);
        }

        /// <summary>
        /// request a texture file to appear in the cache.
        /// </summary>
        /// <param name="ent">Entity the provides context for the request (asset server)</param>
        /// <param name="worldID">The world ID for the requested texture</param>
        /// <param name="finishCall">Where to call when the texture is in the cache</param>
        // TODO: if we get a request for the same texture by two different routines
        // at the same time, this doesn't do all the callbacks
        // To enable this feature, remove the dictionary and checks for already fetching
        public override async Task<IAssetContext.AssetLoadInfo> DoTextureLoad(EntityName textureEntityName, AssetType typ) {

            EntityNameLL textureEnt = new EntityNameLL(textureEntityName);
            string worldID = textureEnt.EntityPart;
            OMV.UUID binID = new OMV.UUID(worldID);

            // do we already have the file?
            string textureFilename = Path.Combine(CacheDirBase, textureEnt.CacheFilename);
            lock (FileSystemAccessLock) {
                if (File.Exists(textureFilename)) {
                    m_log.Log(MBLogLevel.DTEXTUREDETAIL, "DoTextureLoad: Texture file already exists for " + worldID);
                    byte[] data = File.ReadAllBytes(textureFilename);
                    OMV.Assets.AssetTexture assetTexture = new OMV.Assets.AssetTexture(OMV.UUID.Zero, data);
                    bool hasTransparancy = IAssetContext.CheckAssetTextureForTransparancy(assetTexture);

                    return new IAssetContext.AssetLoadInfo(textureEntityName, typ,
                                                            OMV.TextureRequestState.Finished,
                                                            assetTexture) {
                        HasTransparancy = hasTransparancy
                    };
                }
            }

            return await DoTextureLoadInternal(binID, textureEntityName, typ, textureFilename);
        }

        // NEW ======================================
        private async Task<IAssetContext.AssetLoadInfo> DoTextureLoadInternal(OMV.UUID binID,
                            EntityName pTextureEntityName,
                            OMV.AssetType typ,
                            string pTextureFilename) {

            m_queuedTextureRequests.Increment();
            await m_textureThrottle.WaitAsync();
            m_queuedTextureRequests.Decrement();
            m_activeTextureRequests.Increment();

            try {
                var tcs = new TaskCompletionSource<IAssetContext.AssetLoadInfo>();

                // Store callback target for OnACDownloadFinished
                IAssetContext.WaitingInfo? existingWi = null;
                lock (m_waiting) {
                    if (!m_waiting.ContainsKey(binID)) {
                        IAssetContext.WaitingInfo wi = new IAssetContext.WaitingInfo(binID) {
                            type = typ,
                            filename = pTextureFilename,
                            tcs = tcs
                        };
                        m_waiting.Add(binID, wi);
                    } else {
                        // Already being fetched, wait on same TaskCompletionSource
                        existingWi = m_waiting[binID];
                    }
                }
                // If already being fetched, wait for completion outside lock
                if (existingWi != null && existingWi.tcs != null) {
                    return await existingWi.tcs.Task;
                }

                m_gridClient.Assets.RequestImage(binID, OMV.ImageType.Normal, OnACDownloadFinished, false);
                return await tcs.Task;
            } finally {
                m_activeTextureRequests.Decrement();
                m_textureThrottle.Release();
            }
        }

        // Used for texture pipeline
        // returns flag = true if texture was sucessfully downloaded
        private void OnACDownloadFinished(OMV.TextureRequestState state, OMV.Assets.AssetTexture assetTexture) {
            OMV.UUID binID = assetTexture.AssetID;

            lock (m_waiting) {
                if (m_waiting.TryGetValue(binID, out IAssetContext.WaitingInfo? wi)) {
                    m_log.Log(MBLogLevel.DTEXTUREDETAIL, "OnACDownloadFinished: Unknown texture download finished: " + binID.ToString());
                    // TODO: should this text for transparancy be in the caller?
                    bool hasTransparancy = IAssetContext.CheckAssetTextureForTransparancy(assetTexture);
                    var result = new IAssetContext.AssetLoadInfo(
                                        ConvertToEntityName(this, binID.ToString()),
                                        wi.type,
                                        state,
                                        assetTexture) {
                        HasTransparancy = hasTransparancy
                    };
                    wi.tcs?.SetResult(result);
                    m_waiting.Remove(binID);
                }
            }
        }

        public OMVSD.OSD GetDisplayable() {
            return m_stats.GetDisplayable();
        }

        public override void Dispose() {
            base.Dispose();
        }

    }
}
