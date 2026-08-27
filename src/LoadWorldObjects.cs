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

    /// <summary>
    /// If we get started up after OpenMetaverse as been logged in, we must
    /// suck the state out of the OpenMetaverse library and push it into
    /// our world representation.
    /// </summary>
    /// 
    public class LoadWorldObjects {
        private MBLogger<LoadWorldObjects> m_log;

        public LoadWorldObjects(MBLogger<LoadWorldObjects> pLogger) {
            m_log = pLogger;
        }

        public void Load(LMV.GridClient netComm, CommLLLP worldComm) {
            m_log.Log(MBLogLevel.DCOMM, "LoadWorldObjects: loading existing context");
            List<LMV.Simulator> simsToLoad = new List<LMV.Simulator>();
            lock (netComm.Network.Simulators) {
                foreach (LMV.Simulator sim in netComm.Network.Simulators) {
                    if (WeDontKnowAboutThisSimulator(sim, netComm, worldComm)) {
                        // tell the world about this simulator
                        m_log.Log(MBLogLevel.DCOMMDETAIL, "LoadWorldObjects: adding simulator {0}", sim.Name);
                        worldComm.Network_SimConnected(netComm, new LMV.SimConnectedEventArgs(sim));
                        simsToLoad.Add(sim);
                    }
                }
            }
            Object[] loadParams = { simsToLoad, netComm, worldComm };
            Task.Run(() => LoadSims(loadParams));
            m_log.Log(MBLogLevel.DCOMM, "LoadWorldObjects: started thread to load sim objects");
        }

        /// <summary>
        /// Routine called on a separate thread to load the avatars and objects from the simulators
        /// into KeeKee.
        /// </summary>
        /// <param name="loadParam"></param>
        private void LoadSims(Object loadParam) {
            m_log.Log(MBLogLevel.DCOMM, "LoadWorldObjects: starting to load sim objects");
            try {
                Object[] loadParams = (Object[])loadParam;
                List<LMV.Simulator> simsToLoad = (List<LMV.Simulator>)loadParams[0];
                LMV.GridClient netComm = (LMV.GridClient)loadParams[1];
                CommLLLP worldComm = (CommLLLP)loadParams[2];

                LMV.Simulator? simm = null;
                try {
                    foreach (LMV.Simulator sim in simsToLoad) {
                        simm = sim;
                        LoadASim(sim, netComm, worldComm);
                    }
                } catch (Exception e) {
                    m_log.Log(MBLogLevel.DBADERROR, "LoadWorldObjects: exception loading {0}: {1}",
                        simm == null ? "NULL" : simm.Name, e.ToString());
                }
            } catch (Exception e) {
                m_log.Log(MBLogLevel.DBADERROR, "LoadWorldObjects: exception: {0}", e.ToString());
            }
            m_log.Log(MBLogLevel.DCOMM, "LoadWorldObjects: completed loading sim objects");
        }

        public void LoadASim(LMV.Simulator sim, LMV.GridClient netComm, CommLLLP worldComm) {
            m_log.Log(MBLogLevel.DCOMM, "LoadWorldObjects: loading avatars and objects for sim {0}", sim.Name);
            AddAvatars(sim, netComm, worldComm);
            AddObjects(sim, netComm, worldComm);
        }

        // Return 'true' if we don't have this region in our world yet
        private static bool WeDontKnowAboutThisSimulator(LMV.Simulator sim, LMV.GridClient netComm, CommLLLP worldComm) {
            LLRegionContext? regn = worldComm.FindRegion(sim);
            return (regn == null);
        }

        private void AddAvatars(LMV.Simulator sim, LMV.GridClient netComm, CommLLLP worldComm) {
            m_log.Log(MBLogLevel.DCOMM, "LoadWorldObjects: loading {0} avatars", sim.ObjectsAvatars.Count);
            List<LMV.Avatar> avatarsToNew = new List<LMV.Avatar>(sim.ObjectsAvatars.Values);
            // this happens outside the avatar list lock
            avatarsToNew.ForEach(av => {
                worldComm.Objects_AvatarUpdate(netComm, new LMV.AvatarUpdateEventArgs(sim, av, 0, true));
            });
        }

        private void AddObjects(LMV.Simulator sim, LMV.GridClient netComm, CommLLLP worldComm) {
            m_log.Log(MBLogLevel.DCOMM, "LoadWorldObjects: loading {0} primitives", sim.ObjectsPrimitives.Count);
            List<LMV.Primitive> primsToNew = new List<LMV.Primitive>(sim.ObjectsPrimitives.Values);
            // this happens outside the primitive list lock
            primsToNew.ForEach(prim => {
                worldComm.Objects_ObjectUpdate(netComm, new LMV.PrimEventArgs(sim, prim, 0, true, false));
            });
        }
    }
}
