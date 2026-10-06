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

using org.herbal3d.mblue.ecm;
using org.herbal3d.mblue.Logging;

using LMV = LibreMetaverse;

namespace org.herbal3d.mblue.comm.os {
    public class CmptOSAvatar : ComponentBase<CmptOSAvatar>, ICmptAvatar {

        private MBLogger<CmptOSAvatar> m_log;
        private OSGridClient m_client;

        public LMV.UUID? AvatarUUID { get; set; }

        public string DisplayName { get; set; } = "";
        public string ActivityFlags { get; set; } = ""; // e.g. "Sitting, AFK"

        public CmptOSAvatar(MBLogger<CmptOSAvatar> pLog,
                                OSGridClient pClient,
                                IEntity pContainingEntity
                                ) : base(pContainingEntity) {
            m_log = pLog;
            m_client = pClient;
        }

        public string FullName {
            get {
                if (AvatarUUID.HasValue) {
                    var avName = m_client.GridClient.Self.Name;
                    if (avName != null) {
                        return avName.ToString();
                    } else {
                        return $"FullName: Unknown Avatar {AvatarUUID.ToString()}";
                    }
                } else {
                    return "No Avatar UUID";
                }
            }
        }
        public string First {
            get {
                if (AvatarUUID.HasValue) {
                    var avName = m_client.GridClient.Self.FirstName;
                    if (avName != null) {
                        return avName.ToString();
                    } else {
                        return $"First: Unknown Avatar {AvatarUUID.ToString()}";
                    }
                } else {
                    return "No Avatar UUID";
                }
            }
        }
        public string Last {
            get {
                if (AvatarUUID.HasValue) {
                    var avName = m_client.GridClient.Self.LastName;
                    if (avName != null) {
                        return avName.ToString();
                    } else {
                        return $"Last: Unknown Avatar {AvatarUUID.ToString()}";
                    }
                } else {
                    return "No Avatar UUID";
                }
            }
        }

        public override void Dispose() {
            base.Dispose();
        }

        public void Update(UpdateCodes what) {
            return;
        }
    }
}

