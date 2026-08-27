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

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using org.herbal3d.mblue.comm.os;
using org.herbal3d.mblue.Config;

namespace org.herbal3d.mblue.comm {

    public static class MBlueCommOSServiceSetup {

        public static IServiceCollection AddServices(this IServiceCollection pServices, IConfiguration pConfig) {
            return pServices
                .Configure<CommOSConfig>(pConfig.GetSection(CommOSConfig.subSectionName))
                .Configure<GridConfig>(pConfig.GetSection(GridConfig.subSectionName))
                .AddSingleton<LLGridClient>()
                .AddSingleton<LLAssetContext>()
                .AddSingleton<ICommProvider, CommLLLP>()
            ;
        }
    }

}
