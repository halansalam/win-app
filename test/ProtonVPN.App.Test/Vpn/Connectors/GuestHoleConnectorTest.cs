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
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using ProtonVPN.Common.Networking;
using ProtonVPN.Common.Storage;
using ProtonVPN.Common.Vpn;
using ProtonVPN.Core.Api;
using ProtonVPN.Core.Servers.Contracts;
using ProtonVPN.Core.Service.Vpn;
using ProtonVPN.Core.Settings;
using ProtonVPN.Core.Vpn;
using ProtonVPN.Vpn.Connectors;

namespace ProtonVPN.App.Test.Vpn.Connectors
{
    [TestClass]
    public class GuestHoleConnectorTest
    {
        private const int MAX_RETRIES = 2;

        private GuestHoleConnector _connector;
        private readonly IVpnServiceManager _serviceManager = Substitute.For<IVpnServiceManager>();
        private readonly IAppSettings _appSettings = Substitute.For<IAppSettings>();
        private readonly Common.Configuration.Config _config = new()
        {
            MaxGuestHoleRetries = MAX_RETRIES,
            GuestHoleVpnUsername = "guest",
            GuestHoleVpnPassword = "guest",
            VpnUsernameSuffix = "+pw"
        };
        private readonly ICollectionStorage<GuestHoleServerContract> _guestHoleServers =
            Substitute.For<ICollectionStorage<GuestHoleServerContract>>();

        [TestInitialize]
        public void Initialize()
        {
            GuestHoleState guestHoleState = new();
            guestHoleState.SetState(true);

            _guestHoleServers.GetAll().Returns(new List<GuestHoleServerContract>());
            _connector = new GuestHoleConnector(_serviceManager, _appSettings, guestHoleState, _config, _guestHoleServers);
        }

        [TestMethod]
        public async Task ItShouldConnectWithGuestCredentials()
        {
            // Arrange
            string expectedUsername = _config.GuestHoleVpnUsername + _config.VpnUsernameSuffix;

            // Act
            await _connector.Connect();

            // Assert
            await _serviceManager.Received(1)
                .Connect(Arg.Is<VpnConnectionRequest>(c =>
                    c.Credentials.Username == expectedUsername &&
                    c.Credentials.Password == _config.GuestHoleVpnPassword));
        }

        [TestMethod]
        public async Task ItShouldDisconnectAfterCoupleOfRetries()
        {
            // Act
            for (int i = 0; i < MAX_RETRIES; i++)
            {
                await _connector.OnVpnStateChanged(GetEventArgs());
            }

            // Assert
            await _serviceManager.Received(1).Disconnect(VpnError.NoneKeepEnabledKillSwitch);
        }

        private VpnStateChangedEventArgs GetEventArgs() =>
            new(VpnStatus.Reconnecting,
                VpnError.None,
                string.Empty,
                true,
                VpnProtocol.Smart,
                OpenVpnAdapter.Tun);
    }
}