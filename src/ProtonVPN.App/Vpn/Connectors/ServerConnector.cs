﻿/*
 * Copyright (c) 2020 Proton Technologies AG
 *
 * This file is part of ProtonVPN.
 *
 * ProtonVPN is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * ProtonVPN is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with ProtonVPN.  If not, see <https://www.gnu.org/licenses/>.
 */

using System.Threading.Tasks;
using ProtonVPN.Core.Profiles;
using ProtonVPN.Core.Servers;
using ProtonVPN.Core.Servers.Models;
using ProtonVPN.Core.Service.Vpn;

namespace ProtonVPN.Vpn.Connectors
{
    public class ServerConnector : BaseConnector
    {
        public ServerConnector(IVpnManager vpnManager) : base(vpnManager)
        {
        }

        public async Task Connect(Server server)
        {
            var profile = new Profile
            {
                IsTemporary = true,
                ProfileType = ProfileType.Custom,
                Features = (Features)server.Features,
                ServerId = server.Id
            };

            await VpnManager.ConnectAsync(profile);
        }
    }
}
