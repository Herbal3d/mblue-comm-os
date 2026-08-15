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

using KeeKee.Framework;
using KeeKee.Framework.Statistics;

using OMVSD = OpenMetaverse.StructuredData;

namespace org.herbal3d.mblue.comm {
    public class CommLLLPStats : IDumpable {

        // Statistics ===============================================
        // ICommProvider.CommStatistics
        public StatisticCollection CommStatistics { get; private set; }

        public Stat<long> NetDisconnected = new StatCounter("Network_Disconnected", "Number of 'network disconnected' messages");
        public Stat<long> NetLoginProgress = new StatCounter("Network_LoginProgress", "Number of 'login progress' messages");
        public Stat<long> NetSimChanged = new StatCounter("Network_SimChanged", "Number of 'sim changed' messages");
        public Stat<long> NetSimConnected = new StatCounter("Network_SimConnected", "Number of 'sim connected' messages");
        public Stat<long> NetEventQueueRunning = new StatCounter("Network_EventQueueRunning", "Number of 'event queue running' messages");
        public Stat<long> ObjAttachmentUpdate = new StatCounter("Object_AttachmentUpdate", "Number of 'attachment update' messages");
        public Stat<long> ObjAvatarUpdate = new StatCounter("Object_AvatarUpdate", "Number of 'avatar update' messages");
        public Stat<long> ObjKillObject = new StatCounter("Object_KillObject", "Number of 'kill object' messages");
        public Stat<long> ObjObjectProperties = new StatCounter("Object_ObjectProperties", "Number of 'object properties' messages");
        public Stat<long> ObjObjectPropertiesUpdate = new StatCounter("Object_ObjectPropertiesUpdate", "Number of 'object properties update' messages");
        public Stat<long> ObjObjectUpdate = new StatCounter("Object_ObjectUpdate", "Number of 'object update' messages");
        public Stat<long> ObjTerseUpdate = new StatCounter("Object_TerseObjectUpdate", "Number of 'terse object update' messages");
        public Stat<long> RequestLocalID = new StatCounter("RequestLocalID", "Number of RequestLocalIDs made");
        // ==========================================================

        public CommLLLPStats() {
            CommStatistics = new StatisticCollection();
            CommStatistics.AddStat(NetDisconnected);
            CommStatistics.AddStat(NetLoginProgress);
            CommStatistics.AddStat(NetSimChanged);
            CommStatistics.AddStat(NetSimConnected);
            CommStatistics.AddStat(NetEventQueueRunning);
            CommStatistics.AddStat(ObjAttachmentUpdate);
            CommStatistics.AddStat(ObjAvatarUpdate);
            CommStatistics.AddStat(ObjKillObject);
            CommStatistics.AddStat(ObjObjectProperties);
            CommStatistics.AddStat(ObjObjectPropertiesUpdate);
            CommStatistics.AddStat(ObjObjectUpdate);
            CommStatistics.AddStat(ObjTerseUpdate);
            CommStatistics.AddStat(RequestLocalID);
        }

        public OMVSD.OSD? GetDisplayable() {
            return CommStatistics.GetDisplayable();
        }
    }
}

