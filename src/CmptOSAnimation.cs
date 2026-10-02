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
    public class CmptOSAnimation : ComponentBase<CmptOSAnimation> {

        private MBLogger<CmptOSAnimation> m_log;
        private LMV.IGridClient m_client;

        // for the moment, there is not much to an animation
        public LMV.Vector3 AngularVelocity { get; set; } = LMV.Vector3.Zero;

        public bool DoStaticRotation { get; set; } = false;
        public LMV.Vector3 StaticRotationAxis { get; set; } = LMV.Vector3.Zero;
        public float StaticRotationRotPerSec { get; set; } = 0f;

        public CmptOSAnimation(MBLogger<CmptOSAnimation> pLog,
                                LMV.IGridClient pClient,
                                IEntity pContainingEntity
                                ) : base(pContainingEntity) {
            m_log = pLog;
            m_client = pClient;
            ContainingEntity = pContainingEntity;
        }
    }
}
