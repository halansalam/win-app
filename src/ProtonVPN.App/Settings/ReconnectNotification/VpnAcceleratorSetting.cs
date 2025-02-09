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

using System.Collections.Generic;
using ProtonVPN.Common.Networking;
using ProtonVPN.Core.Settings;

namespace ProtonVPN.Settings.ReconnectNotification
{
    public class VpnAcceleratorSetting : SingleSetting
    {
        private readonly SingleSetting _vpnAcceleratorSetting;
        private readonly IAppSettings _appSettings;

        public VpnAcceleratorSetting(string name, Setting parent, IAppSettings appSettings) : base(name, parent, appSettings)
        {
            _appSettings = appSettings;
            _vpnAcceleratorSetting = new SingleSetting(nameof(IAppSettings.VpnAcceleratorEnabled), this, appSettings);
        }

        public override List<Setting> GetChildren()
        {
            return new() {_vpnAcceleratorSetting};
        }

        public override bool Changed()
        {
            return base.Changed() && _appSettings.GetProtocol() != VpnProtocol.WireGuard;
        }
    }
}