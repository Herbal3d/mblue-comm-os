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

using LMV = LibreMetaverse;

using org.herbal3d.mblue.Logging;

namespace org.herbal3d.mblue.comm {

    /// <summary>
    /// Singleton class to hold the creation and instance of GridClient for LLLP communication.
    /// Makes it easy to get the reference to the GridClient into instances.
    /// </summary>
    public sealed class LLGridClient {
        private MBLogger<LLGridClient> m_Log;
        public LLGridClient(MBLogger<LLGridClient> pLog) {
            m_Log = pLog;
        }

        public LMV.GridClient? m_GridClient = null;
        public LMV.GridClient GridClient {
            get {
                if (m_GridClient == null) {
                    m_Log.Log(MBLogLevel.Information, "Creating new GridClient");
                    m_GridClient = new LMV.GridClient();
                }
                return m_GridClient;
            }
        }
    }
}
