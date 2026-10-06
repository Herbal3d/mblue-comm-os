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

using org.herbal3d.mblue.Common.StructuredData;
using org.herbal3d.mblue.ecm;
using org.herbal3d.mblue.Logging;

using LMV = LibreMetaverse;

namespace org.herbal3d.mblue.comm.os {
    public class CmptOSLocation : ComponentBase<CmptOSLocation> {

        private MBLogger<CmptOSLocation> m_log;
        private OSGridClient m_client;

        public CmptOSLocation(MBLogger<CmptOSLocation> pLog,
                            OSGridClient pClient,
                            IEntity pContainingEntity) : base(pContainingEntity) {
            m_log = pLog;
            m_client = pClient;
        }

        private bool m_haveLocalHeading = false;
        private Quaternion m_heading;
        public Quaternion Heading {
            get {
                // kludge to allow the local agent to be different for dead reconning
                if (m_haveLocalHeading) {
                    return m_heading;
                }
                var sr = m_client.GridClient.Self.SimRotation;
                return new Quaternion(sr.X, sr.Y, sr.Z, sr.W);
            }
            set {
                m_heading = value;
                m_haveLocalHeading = true;
            }
        }

        public Vector3 LocalPosition {
            get {
                var lp = this.LocalPosition;
                return new Vector3(lp.X, lp.Y, lp.Z);
            }
            set {
                // If an object, we can set the position directly
                // m_client.Self.Position = value;
            }
        }

        public Vector3 RegionPosition {
            get {
                var lp = this.LocalPosition;
                return new Vector3(lp.X, lp.Y, lp.Z);
            }
            set {
                // If an object, we can set the position directly
                // m_client.Self.Position = value;
            }
        }

        public Vector3d GlobalPosition {
            get {
                var gp = m_client.GridClient.Self.GlobalPosition;
                return new Vector3d(gp.X, gp.Y, gp.Z);
                // return AssociatedAvatar.RegionContext.CalculateGlobalPosition(RelativePosition);
            }
        }

        public override void Dispose() {
            base.Dispose();
        }
    }
}

