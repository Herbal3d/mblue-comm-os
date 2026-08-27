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

using System.Globalization;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Options;

using org.herbal3d.mblue.Config;
using org.herbal3d.mblue.ecm;
using org.herbal3d.mblue.Logging;

using LMV = LibreMetaverse;

namespace org.herbal3d.mblue.comm.os {

    /// <summary>
    /// Component that is added to an entity that is from the CommOS system.
    /// This provides the pointers to the underlying CommOS system components,
    /// such as the GridClient and TerrainInfo.
    /// </summary>
    public class CmptOSInfo : ComponentBase {
        private MBLogger<CmptOSInfo> m_log;
        private LMV.GridClient m_client;
        private IOptions<CommOSConfig> m_config;

        // The region the entity is in
        public LMV.Simulator? Region { get; set; }
        // If the entity is terrain, this will hold dimensions and processing routines
        public TerrainInfo? Terrain { get; set; }

        public CmptOSInfo(MBLogger<CmptOSInfo> pLog,
                        LMV.GridClient pClient,
                        IOptions<CommOSConfig> pConfig)
                        : base("CmptOSInfo") {
            m_log = pLog;
            m_client = pClient;
            m_config = pConfig;
        }

        public override JsonNode? GetDump() {
            return null;
        }

        public override void Dispose() {
            return;
        }
    }
}


