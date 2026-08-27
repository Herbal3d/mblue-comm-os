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

namespace org.herbal3d.mblue.Config {
    public class GridConfig : List<GridDefinition> {
        public static string subSectionName { get; } = "Grids";
    }

    public class GridDefinition {
        public string GridName { get; set; } = "SecondLife";
        public string GridNick { get; set; } = "SecondLife";
        // URI to use for login. Accepts HTTP POST of login form.
        public string LoginURI { get; set; } = "https://login.angi.lindenlab.com/cgi-bin/login.cgi";
        // Helper for support
        public string? SupportURI { get; set; } = "https://secondlife.com/";
        // helper for account management
        public string? AccountURI { get; set; } = "https://secondlife.com/";
        // Helper for password reset
        public string? PasswordURI { get; set; } = "https://secondlife.com/";
        // The main website for the grid
        public string? WebSite { get; set; } = "https://secondlife.com/";
        // Name of the platform (SecondLife, OpenSim, etc)
        public string? Platform { get; set; } = "SecondLife";
        // Base URL for SLURLs (hop://maps.secondlife.com/secondlife/Region/X/Y/Z)
        public string? slurl_base { get; set; } = "hop://maps.secondlife.com/secondlife/";
        // Base URL for app SLURLs (secondlife:///app/...)
        public string? app_slurl_base { get; set; } = "secondlife:///app";
        // Base URL for web profiles. May include "[AGENT_NAME]" to be replaced with avatar name.
        public string? web_profile_url { get; set; } = "https://my.secondlife.com/";
    }
}

