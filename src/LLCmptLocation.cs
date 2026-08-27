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
    public class LLCmptLocation : ICmptLocation {

        private MBLogger<LLCmptLocation> m_log;
        private LMV.GridClient m_client;
        public IEntity ContainingEntity { get { return ContainingEntity; } }
        public LLEntity ContainingLLEntity { get; private set; }

        public LLCmptLocation(MBLogger<LLCmptLocation> pLog,
                            LLEntity pContainingLLEntity,
                            LLGridClient pClient) {
            m_log = pLog;
            m_client = pClient.GridClient;
            ContainingLLEntity = pContainingLLEntity;
        }

        private bool m_haveLocalHeading = false;
        private LMV.Quaternion m_heading;
        public LMV.Quaternion Heading {
            get {
                // kludge to allow the local agent to be different for dead reconning
                if (m_haveLocalHeading) {
                    return m_heading;
                }
                return m_client.Self.SimRotation;
            }
            set {
                m_heading = value;
                m_haveLocalHeading = true;
            }
        }

        public LMV.Vector3 LocalPosition {
            get {
                return m_client.Self.SimPosition;
            }
            set {
                // If an object, we can set the position directly
                // m_client.Self.Position = value;
            }
        }

        public LMV.Vector3 RegionPosition {
            get {
                return this.LocalPosition;
            }
            set {
                // If an object, we can set the position directly
                // m_client.Self.Position = value;
            }
        }

        public LMV.Vector3d GlobalPosition {
            get {
                return m_client.Self.GlobalPosition;
                // return AssociatedAvatar.RegionContext.CalculateGlobalPosition(RelativePosition);
            }
        }

        public void Update(UpdateCodes what) {
            return;
        }

        public void Dispose() {
            // nothing to do
        }
    }
}

