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

using System;
using System.Windows;
using Autofac;
using Caliburn.Micro;
using ProtonVPN.About;
using ProtonVPN.Account;
using ProtonVPN.BugReporting;
using ProtonVPN.BugReporting.Diagnostic;
using ProtonVPN.Common.Configuration;
using ProtonVPN.Common.Logging;
using ProtonVPN.Common.OS.Processes;
using ProtonVPN.Common.OS.Services;
using ProtonVPN.Common.Storage;
using ProtonVPN.Common.Text.Serialization;
using ProtonVPN.Common.Threading;
using ProtonVPN.Core.Api;
using ProtonVPN.Core.Api.Contracts;
using ProtonVPN.Core.Auth;
using ProtonVPN.Core.Modals;
using ProtonVPN.Core.Profiles;
using ProtonVPN.Core.Profiles.Cached;
using ProtonVPN.Core.Servers;
using ProtonVPN.Core.Servers.Contracts;
using ProtonVPN.Core.Service;
using ProtonVPN.Core.Service.Settings;
using ProtonVPN.Core.Service.Vpn;
using ProtonVPN.Core.Settings;
using ProtonVPN.Core.Startup;
using ProtonVPN.Core.Storage;
using ProtonVPN.Core.User;
using ProtonVPN.Core.Window;
using ProtonVPN.Core.Window.Popups;
using ProtonVPN.Crypto;
using ProtonVPN.FlashNotifications;
using ProtonVPN.Map;
using ProtonVPN.Modals;
using ProtonVPN.Modals.Dialogs;
using ProtonVPN.Notifications;
using ProtonVPN.PlanDowngrading;
using ProtonVPN.Servers;
using ProtonVPN.Settings;
using ProtonVPN.Settings.Migrations;
using ProtonVPN.Settings.ReconnectNotification;
using ProtonVPN.Settings.SplitTunneling;
using ProtonVPN.Sidebar;
using ProtonVPN.Streaming;
using ProtonVPN.Vpn;
using ProtonVPN.Vpn.Connectors;

namespace ProtonVPN.Core.Ioc
{
    public class AppModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.Register(c => new ConfigFactory().Config());

            builder.RegisterType<Bootstrapper>().SingleInstance();
            builder.RegisterType<EventAggregator>().As<IEventAggregator>().SingleInstance();
            builder.RegisterType<JsonSerializerFactory>().As<ITextSerializerFactory>().SingleInstance();

            builder.RegisterType<SidebarManager>().SingleInstance();
            builder.RegisterType<UpdateViewModel>().AsSelf().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<VpnConnectionSpeed>().AsImplementedInterfaces().AsSelf().SingleInstance();

            builder.Register(c => new CollectionCache<LogicalServerContract>(
                    c.Resolve<ILogger>(),
                    c.Resolve<ITextSerializerFactory>(),
                    c.Resolve<Common.Configuration.Config>().ServersJsonCacheFilePath))
                .As<ICollectionStorage<LogicalServerContract>>()
                .SingleInstance();

            builder.Register(c => new CollectionCache<GuestHoleServerContract>(
                    c.Resolve<ILogger>(),
                    c.Resolve<ITextSerializerFactory>(),
                    c.Resolve<Common.Configuration.Config>().GuestHoleServersJsonFilePath))
                .As<ICollectionStorage<GuestHoleServerContract>>()
                .SingleInstance();

            builder.RegisterType<ApiServers>().As<IApiServers>().SingleInstance();
            builder.RegisterType<ServerUpdater>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<AuthCertificateUpdater>().AsImplementedInterfaces().SingleInstance();
            builder.Register(c => new ServerLoadUpdater(
                    c.Resolve<Common.Configuration.Config>().ServerLoadUpdateInterval,
                    c.Resolve<ServerManager>(),
                    c.Resolve<IScheduler>(),
                    c.Resolve<IEventAggregator>(),
                    c.Resolve<IMainWindowState>(),
                    c.Resolve<IApiServers>(),
                    c.Resolve<ISingleActionFactory>(),
                    c.Resolve<ILastServerLoadTimeProvider>()))
                .AsImplementedInterfaces()
                .AsSelf()
                .SingleInstance();
            builder.RegisterType<UserStorage>().As<IUserStorage>().SingleInstance();
            builder.RegisterType<TruncatedLocation>().SingleInstance();

            builder.RegisterType<ServerCandidatesFactory>().SingleInstance();
            builder.RegisterType<PinFactory>()
                .AsImplementedInterfaces()
                .AsSelf()
                .SingleInstance();

            builder.RegisterType<ServerListFactory>().AsImplementedInterfaces().AsSelf().SingleInstance();
            builder.RegisterInstance((App)Application.Current).SingleInstance();
            builder.RegisterType<VpnService>().AsSelf().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<ModalWindows>().As<IModalWindows>().SingleInstance();
            builder.RegisterType<ProtonVPN.Modals.Modals>().As<IModals>().SingleInstance();
            builder.RegisterType<Windows.Popups.PopupWindows>().As<IPopupWindows>().SingleInstance();
            builder.RegisterType<Dialogs>().As<IDialogs>().SingleInstance();
            builder.RegisterType<AutoStartup>().As<IAutoStartup>().SingleInstance();
            builder.RegisterType<SyncableAutoStartup>().As<ISyncableAutoStartup>().SingleInstance();
            builder.RegisterType<SyncedAutoStartup>().AsSelf().As<ISettingsAware>().SingleInstance();

            builder.RegisterType<AppSettingsStorage>().SingleInstance();
            builder.Register(c =>
                    new EnumerableAsJsonSettings(
                        new CachedSettings(
                            new EnumAsStringSettings(
                                new SelfRepairingSettings(
                                    c.Resolve<AppSettingsStorage>())))))
                .As<ISettingsStorage>()
                .SingleInstance();

            builder.RegisterType<PerUserSettings>()
                .AsSelf()
                .As<ILoggedInAware>()
                .As<ILogoutAware>()
                .SingleInstance();
            builder.RegisterType<UserSettings>().SingleInstance();

            builder.RegisterType<PredefinedProfiles>().SingleInstance();
            builder.RegisterType<CachedProfiles>().SingleInstance();
            builder.RegisterType<ApiProfiles>().SingleInstance();
            builder.RegisterType<Profiles.Profiles>().SingleInstance();
            builder.RegisterType<SyncProfiles>().AsSelf().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<SyncProfile>().SingleInstance();

            builder.RegisterType<AppSettings>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<ProtonVPN.Settings.Migrations.v1_7_2.AppSettingsMigration>().AsImplementedInterfaces()
                .SingleInstance();
            builder.RegisterType<ProtonVPN.Settings.Migrations.v1_7_2.UserSettingsMigration>().AsImplementedInterfaces()
                .SingleInstance();
            builder.RegisterType<ProtonVPN.Settings.Migrations.v1_8_0.AppSettingsMigration>().AsImplementedInterfaces()
                .SingleInstance();
            builder.Register(c => new ProtonVPN.Settings.Migrations.v1_8_0.UserSettingsMigration(
                    c.Resolve<ISettingsStorage>(),
                    c.Resolve<UserSettings>()))
                .As<IUserSettingsMigration>()
                .SingleInstance();
            builder.RegisterType<ProtonVPN.Settings.Migrations.v1_10_0.AppSettingsMigration>().AsImplementedInterfaces()
                .SingleInstance();
            builder.RegisterType<ProtonVPN.Settings.Migrations.v1_17_0.AppSettingsMigration>().AsImplementedInterfaces()
                .SingleInstance();
            builder.RegisterType<ProtonVPN.Settings.Migrations.v1_18_3.AppSettingsMigration>().AsImplementedInterfaces()
                .SingleInstance();
            builder.RegisterType<ProtonVPN.Settings.Migrations.v1_20_0.AppSettingsMigration>().AsImplementedInterfaces()
                .SingleInstance();
            builder.RegisterType<ProtonVPN.Settings.Migrations.v1_22_0.AppSettingsMigration>().AsImplementedInterfaces()
                .SingleInstance();

            builder.RegisterType<MapLineManager>().AsImplementedInterfaces().AsSelf().SingleInstance();
            builder.RegisterType<VpnEvents>();
            builder.RegisterType<SettingsServiceClientManager>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<SettingsServiceClient>().SingleInstance();
            builder.RegisterType<ServiceChannelFactory>().SingleInstance();
            builder.RegisterType<SettingsContractProvider>().SingleInstance();
            builder.RegisterType<AutoConnect>().SingleInstance();
            builder.RegisterInstance(Properties.Settings.Default);

            builder.Register(c => new ServiceRetryPolicy(2, TimeSpan.FromSeconds(2))).SingleInstance();
            builder.Register(c =>
                    new VpnSystemService(
                        new ReliableService(
                            c.Resolve<ServiceRetryPolicy>(),
                            new SafeService(
                                new LoggingService(
                                    c.Resolve<ILogger>(),
                                    new SystemService(
                                        c.Resolve<Common.Configuration.Config>().ServiceName,
                                        c.Resolve<IOsProcesses>()))))))
                .SingleInstance();
            builder.Register(c =>
                    new AppUpdateSystemService(
                        new ReliableService(
                            c.Resolve<ServiceRetryPolicy>(),
                            new SafeService(
                                new LoggingService(
                                    c.Resolve<ILogger>(),
                                    new SystemService(
                                        c.Resolve<Common.Configuration.Config>().UpdateServiceName,
                                        c.Resolve<IOsProcesses>()))))))
                .SingleInstance();

            builder.RegisterType<VpnServiceManager>().SingleInstance();
            builder.Register(c => new ServiceStartDecorator(
                    c.Resolve<ILogger>(),
                    c.Resolve<VpnServiceManager>(),
                    c.Resolve<IModals>(),
                    c.Resolve<BaseFilteringEngineService>(),
                    c.Resolve<VpnSystemService>()))
                .As<IVpnServiceManager>().SingleInstance();
            builder.RegisterType<VpnManager>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<VpnReconnector>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<VpnConnector>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<SimilarServerCandidatesGenerator>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<ServerConnector>().SingleInstance();
            builder.RegisterType<ProfileConnector>().SingleInstance();
            builder.RegisterType<CountryConnector>().SingleInstance();
            builder.RegisterType<GuestHoleConnector>().AsImplementedInterfaces().AsSelf().SingleInstance();
            builder.RegisterType<GuestHoleState>().SingleInstance();
            builder.RegisterType<DisconnectError>().AsImplementedInterfaces().AsSelf().SingleInstance();
            builder.RegisterType<VpnStateNotification>()
                .AsImplementedInterfaces()
                .AsSelf()
                .SingleInstance();
            builder.RegisterType<OutdatedAppNotification>().AsImplementedInterfaces().AsSelf().SingleInstance();
            builder.RegisterType<AppExitHandler>().AsImplementedInterfaces().AsSelf().SingleInstance();
            builder.RegisterType<UserLocationService>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<InstalledApps>().SingleInstance();
            builder.RegisterType<Onboarding.Onboarding>().AsSelf().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<SystemNotification>().AsImplementedInterfaces().SingleInstance();
            builder.Register(c => new MonitoredVpnService(
                    c.Resolve<Common.Configuration.Config>(),
                    c.Resolve<VpnSystemService>(),
                    c.Resolve<IVpnManager>()))
                .AsImplementedInterfaces()
                .AsSelf()
                .SingleInstance();
            builder.RegisterType<BaseFilteringEngineService>().SingleInstance();
            builder.Register(c => new UpdateNotification(
                    c.Resolve<Common.Configuration.Config>().UpdateRemindInterval,
                    c.Resolve<ISystemNotification>(),
                    c.Resolve<UserAuth>(),
                    c.Resolve<IEventAggregator>(),
                    c.Resolve<UpdateFlashNotificationViewModel>()))
                .AsImplementedInterfaces()
                .SingleInstance();
            builder.RegisterType<SystemProxyNotification>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<InsecureNetworkNotification>().AsImplementedInterfaces().AsSelf().SingleInstance();
            builder.Register(c => new Language(
                    c.Resolve<IAppSettings>(),
                    c.Resolve<ILanguageProvider>(),
                    c.Resolve<Common.Configuration.Config>().DefaultLocale))
                .AsSelf()
                .AsImplementedInterfaces()
                .SingleInstance();
            builder.Register(c => new LanguageProvider(c.Resolve<ILogger>(),
                    c.Resolve<Common.Configuration.Config>().TranslationsFolder,
                    c.Resolve<Common.Configuration.Config>().DefaultLocale))
                .As<ILanguageProvider>()
                .AsSelf()
                .SingleInstance();
            builder.RegisterType<ExpiredSessionHandler>().AsImplementedInterfaces().AsSelf().SingleInstance();
            builder.RegisterType<ReconnectState>().AsImplementedInterfaces().AsSelf().SingleInstance();
            builder.RegisterType<SettingsBuilder>().SingleInstance();
            builder.RegisterType<SystemState>().As<ISystemState>().SingleInstance();
            builder.RegisterType<ReconnectManager>().AsImplementedInterfaces().AsSelf().SingleInstance();
            builder.Register(c => new VpnInfoChecker(
                c.Resolve<Common.Configuration.Config>(),
                c.Resolve<IEventAggregator>(),
                c.Resolve<IApiClient>(),
                c.Resolve<IUserStorage>(),
                c.Resolve<IScheduler>())).SingleInstance();
            builder.RegisterType<ReportFieldProvider>().As<IReportFieldProvider>().SingleInstance();
            builder.RegisterType<PlanDowngradeHandler>().AsImplementedInterfaces().AsSelf().SingleInstance();
            builder.RegisterType<StreamingServicesUpdater>().AsImplementedInterfaces().AsSelf().SingleInstance();
            builder.RegisterType<StreamingServices>().As<IStreamingServices>().SingleInstance();
            builder.RegisterType<StreamingServicesStorage>().SingleInstance();
            builder.RegisterType<NotificationSender>().As<INotificationSender>().SingleInstance();
            builder.RegisterType<NotificationUserActionHandler>().As<INotificationUserActionHandler>().SingleInstance();

            builder.RegisterType<Ed25519Asn1KeyGenerator>().As<IEd25519Asn1KeyGenerator>().SingleInstance();
            builder.RegisterType<X25519KeyGenerator>().As<IX25519KeyGenerator>().SingleInstance();
            builder.RegisterType<AuthKeyManager>().As<IAuthKeyManager>().SingleInstance();
            builder.RegisterType<AuthCertificateManager>().As<IAuthCertificateManager>().SingleInstance();
            builder.RegisterType<AuthCredentialManager>().As<IAuthCredentialManager>().SingleInstance();
        }
    }
}