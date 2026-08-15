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

namespace org.herbal3d.mblue.comm {

    public class CommOSConfig {
        public static string subSectionName { get; set; } = "CommOS";

        // Whether Comm should hold objects if the parent doesn't exist
        public bool ShouldHoldChildren { get; set; } = true;
        // Wether to connect to multiple sims
        public bool MultipleSims { get; set; } = false;
        // Milliseconds between movement messages sent to server
        public int MovementUpdateInterval { get; set; } = 100;

        // Enable debug messages from the communication layer (usually libremetaverse))
        public bool EnableLowLevelCommDebugging { get; set; } = false;
    }
}
