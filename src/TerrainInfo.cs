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

using org.herbal3d.mblue.ecm;
using org.herbal3d.mblue.Logging;

using LMV = LibreMetaverse;

namespace org.herbal3d.mblue.comm.os {
    public class TerrainInfo {

        private MBLogger<TerrainInfo> m_log;
        public float[,] HeightMap { get; protected set; } = new float[256, 256];

        public int HeightMapWidth { get; protected set; } = 256;

        public int HeightMapLength { get; protected set; } = 256;

        public float MaximumHeight { get; protected set; } = 4096.0f;
        public float MinimumHeight { get; protected set; } = -4096.0f;

        public int TerrainPatchStride { get; protected set; } = 16;

        // X dimension (E/W)
        public int TerrainPatchWidth { get; protected set; } = 256;

        // Y dimension (N/S)
        public int TerrainPatchLength { get; protected set; } = 256;

        // height of the water
        public const float NOWATER = -113537;   // here because it can't go in the interface (stupid C#)
        public float WaterHeight { get; set; } = NOWATER;

        protected LMV.Simulator? m_simulator;
        public LMV.Simulator? Simulator { get { return m_simulator; } }

        public LLTerrainInfo(MBLogger<TerrainInfo> pLog,
                            LLRegionContext pRContext,
                            LLAssetContext pAContext)
                    : base(pLog, pWorld, pRContext, pAContext) {
            m_log = pLog;
            TerrainPatchStride = 16;
            TerrainPatchLength = 256;
            TerrainPatchWidth = 256;
            UpdateHeightMap(pRContext);
        }

        public override void UpdatePatch(LLRegionContext reg, int x, int y, float[] data) {
            // even though I am passed the data, I rely on Comm.Client to save it for me
            UpdateHeightMap(reg);
            return;
        }

        private void UpdateHeightMap(LLRegionContext reg) {
            int stride = TerrainPatchStride;
            int stride2 = stride * TerrainPatchWidth;

            lock (this) {
                float[,] newHM = new float[TerrainPatchWidth, TerrainPatchLength];
                float minHeight = 999999f;
                float maxHeight = 0f;

                if ((reg == null) || !(reg is LLRegionContext)) {
                    // things are not set up so create a default, flat heightmap
                    m_log.Log(MBLogLevel.DWORLDDETAIL, "LLTerrainInfo: Building default zero terrain");
                    CreateZeroHeight(ref newHM);
                    minHeight = maxHeight = 0f;
                } else {
                    try {
                        LLRegionContext llreg = (LLRegionContext)reg;
                        LMV.Simulator sim = llreg.Simulator;

                        int nullPatchCount = 0;
                        for (int px = 0; px < stride; px++) {
                            for (int py = 0; py < stride; py++) {
                                LMV.TerrainPatch pat = sim.Terrain[px + py * stride];
                                if (pat == null) {
                                    // if no patch, it's all zeros
                                    if (0.0f < minHeight) minHeight = 0.0f;
                                    if (0.0f > maxHeight) maxHeight = 0.0f;
                                    for (int xx = 0; xx < stride; xx++) {
                                        for (int yy = 0; yy < stride; yy++) {
                                            // newHM[(py * stride + yy), (px * stride + xx)] = 0.0f;
                                            newHM[(px * stride + xx), (py * stride + yy)] = 0.0f;
                                        }
                                    }
                                    nullPatchCount++;
                                } else {
                                    for (int xx = 0; xx < stride; xx++) {
                                        for (int yy = 0; yy < stride; yy++) {
                                            float height = pat.Data[xx + yy * stride];
                                            // newHM[(py * stride + yy), (px * stride + xx)] = height;
                                            newHM[(px * stride + xx), (py * stride + yy)] = height;
                                            if (height < minHeight) minHeight = height;
                                            if (height > maxHeight) maxHeight = height;
                                        }
                                    }
                                }
                            }
                        }
                        // m_log.Log(MBLogLevel.DWORLDDETAIL,
                        //         "LLTerrainInfo: UpdateHeightMap: {0} null patches = {1}", sim.Name, nullPatchCount);
                    } catch {
                        // this usually happens when first starting a region
                        m_log.Log(MBLogLevel.DWORLDDETAIL,
                                "LLTerrainInfo: Exception building terrain. Defaulting to zero.");
                        CreateZeroHeight(ref newHM);
                        minHeight = maxHeight = 0f;
                    }
                }
                HeightMap = newHM;
                HeightMapWidth = TerrainPatchWidth;   // X
                HeightMapLength = TerrainPatchLength;
                MinimumHeight = minHeight;
                MaximumHeight = maxHeight;
                m_log.Log(MBLogLevel.DWORLDDETAIL,
                        "LLTerrainInfo: New terrain:"
                        + " min=" + MinimumHeight.ToString()
                        + " max=" + MaximumHeight.ToString()
                    );
            }
        }

        private void CreateZeroHeight(ref float[,] newHM) {
            for (int xx = 0; xx < TerrainPatchWidth; xx++) {
                for (int yy = 0; yy < TerrainPatchLength; yy++) {
                    newHM[xx, yy] = 0f;
                }
            }
        }
    }
}
