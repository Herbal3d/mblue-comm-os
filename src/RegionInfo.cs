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

using org.herbal3d.mblue.Common;
using org.herbal3d.mblue.ecm;

using LMV = LibreMetaverse;

namespace org.herbal3d.mblue.comm.os;

public class RegionInfo {
    public string Name { get; set; } = string.Empty;

    public LMV.Simulator? Simulator { get; set; } = null;

    public IEntity? TerrainEntity { get; set; } = null;

    public RegionInfo(LMV.Simulator pNewSim) {
        Name = pNewSim.Name;
        Simulator = pNewSim;

        TerrainEntity = null;
    }
}

public class RegionCollection {
    private DualIndexDictionary<LMV.Simulator, LMV.UUID, RegionInfo> m_regionInfo = new DualIndexDictionary<LMV.Simulator, LMV.UUID, RegionInfo>();

    public RegionInfo? GetRegionInfo(LMV.Simulator simulator) {
        if (m_regionInfo.TryGetValue(simulator, out var regionInfo)) {
            return regionInfo;
        }
        return null;
    }

    public bool HasRegionInfo(LMV.Simulator simulator) {
        return m_regionInfo.ContainsKey(simulator);
    }

    public bool HasRegionInfo(LMV.UUID regionId) {
        return m_regionInfo.ContainsKey(regionId);
    }

    public bool TryGetRegionInfo(LMV.Simulator simulator, out RegionInfo? regionInfo) {
        return m_regionInfo.TryGetValue(simulator, out regionInfo);
    }

    public bool TryGetRegionInfo(LMV.UUID pID, out RegionInfo? regionInfo) {
        return m_regionInfo.TryGetValue(pID, out regionInfo);
    }

    public void AddRegionInfo(RegionInfo pRegionInfo) {
        if (pRegionInfo.Simulator == null) return;
        m_regionInfo.Add(pRegionInfo.Simulator, pRegionInfo.Simulator.RegionID, pRegionInfo);
    }
}

