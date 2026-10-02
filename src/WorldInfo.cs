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

using System.Text.Json.Nodes;

using Microsoft.Extensions.Options;

using org.herbal3d.mblue.Config;
using org.herbal3d.mblue.ecm;
using org.herbal3d.mblue.Logging;

using LMV = LibreMetaverse;

namespace org.herbal3d.mblue.comm.os {

    /// <summary>
    /// Holds information about the world, including regions and grid info.
    /// </summary>
    public class WorldInfo {

        private Dictionary<LMV.Simulator, RegionInfo> m_regionInfo = new Dictionary<LMV.Simulator, RegionInfo>();

        public RegionInfo? GetRegionInfo(LMV.Simulator pSim) {
            if (m_regionInfo.TryGetValue(pSim, out RegionInfo? regionInfo)) {
                return regionInfo;
            }
            return null;
        }
    }
}
