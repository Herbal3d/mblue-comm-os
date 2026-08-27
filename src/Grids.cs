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

using org.herbal3d.mblue.Config;
using org.herbal3d.mblue.Logging;

namespace org.herbal3d.mblue.comm.os;

/// <summary>
/// Keeps a list of the possible grids and returns info as requested
/// </summary>
public class Grids {
    private MBLogger<Grids> m_log;

    public string CurrentGrid { get { return m_currentGrid; } }
    private string m_currentGrid = "UnknownXXYYZZ";
    private IOptions<GridConfig> m_gridConfig;

    public Grids(MBLogger<Grids> pLog,
                IOptions<GridConfig> pGridConfig) {
        m_log = pLog;
        m_gridConfig = pGridConfig;
    }

    // set the grid name so Grids.Current works
    public void SetCurrentGrid(string currentGrid) {
        m_currentGrid = currentGrid;
    }

    public string? GridLoginURI(string gridName) {
        var gridDef = GetGridDefinition(gridName);
        return gridDef?.LoginURI ?? "";
    }

    /// <summary>
    /// Fetch the grid definition for the named grid.
    /// Case insensitive..
    /// </summary>
    /// <param name="gridName"></param>
    /// <returns></returns>
    public GridDefinition? GetGridDefinition(string gridName) {
        GridDefinition? ret = null;
        try {
            ForEach((gd) => {
                if (gd.GridNick.ToLower() == gridName.ToLower())
                    ret = gd;
                else if (gd.GridName.ToLower() == gridName.ToLower())
                    ret = gd;
            });
        } catch (Exception e) {
            m_log.Log(MBLogLevel.DBADERROR, "GridList.GetGridDefinition: Exception: {0}", e.ToString());
        }
        return ret;
    }

    // Performs an action on each map which describes a grid ("Name", "LoginURL", ...)
    public void ForEach(Action<GridDefinition> act) {
        try {
            foreach (var gd in m_gridConfig.Value.ToArray()) {
                act(gd);
            }
            ;
        } catch (Exception e) {
            m_log.Log(MBLogLevel.DBADERROR, "GridList.ForEach: Exception: {0}", e.ToString());
        }
    }
}
