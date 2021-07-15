﻿/*
 * Copyright (c) 2021 Proton Technologies AG
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

using System.Net;
using ProtonVPN.Common.Configuration;
using ProtonVPN.Common.Os.Net;
using ProtonVPN.Service.Settings;

namespace ProtonVPN.Service.SplitTunneling
{
    internal class BestNetworkInterface
    {
        private readonly Common.Configuration.Config _config;
        private readonly IServiceSettings _serviceSettings;

        public BestNetworkInterface(Common.Configuration.Config config, IServiceSettings serviceSettings)
        {
            _config = config;
            _serviceSettings = serviceSettings;
        }

        public IPAddress LocalIpAddress()
        {
            return NetworkUtil.GetBestInterfaceIp(_config.GetHardwareId(_serviceSettings.OpenVpnAdapter));
        }
    }
}