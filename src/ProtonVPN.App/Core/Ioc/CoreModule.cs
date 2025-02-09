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

using System;
using System.Net.Http;
using Autofac;
using Caliburn.Micro;
using ProtonVPN.Common.Extensions;
using ProtonVPN.Common.Logging;
using ProtonVPN.Common.OS;
using ProtonVPN.Common.OS.Net;
using ProtonVPN.Common.OS.Net.Http;
using ProtonVPN.Common.OS.Net.NetworkInterface;
using ProtonVPN.Common.OS.Processes;
using ProtonVPN.Common.OS.Registry;
using ProtonVPN.Common.Threading;
using ProtonVPN.Config.Url;
using ProtonVPN.Core.Abstract;
using ProtonVPN.Core.Announcements;
using ProtonVPN.Core.Api;
using ProtonVPN.Core.Api.Handlers;
using ProtonVPN.Core.Api.Handlers.TlsPinning;
using ProtonVPN.Core.Auth;
using ProtonVPN.Core.Config;
using ProtonVPN.Core.Events;
using ProtonVPN.Core.HumanVerification;
using ProtonVPN.Core.Network;
using ProtonVPN.Core.OS.Net;
using ProtonVPN.Core.OS.Net.Dns;
using ProtonVPN.Core.OS.Net.DoH;
using ProtonVPN.Core.Servers;
using ProtonVPN.Core.Settings;
using ProtonVPN.Core.Storage;
using ProtonVPN.Core.Threading;
using ProtonVPN.Core.Update;
using ProtonVPN.Core.Window;
using ProtonVPN.HumanVerification;
using ProtonVPN.Modals.ApiActions;
using ProtonVPN.Translations;
using ProtonVPN.Settings;
using ProtonVPN.Vpn;
using Module = Autofac.Module;

namespace ProtonVPN.Core.Ioc
{
    public class CoreModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<HumanVerifier>().As<IHumanVerifier>().SingleInstance();
            builder.RegisterType<ServicePointConfiguration>().SingleInstance();
            builder.RegisterType<ServerManager>().SingleInstance();
            builder.RegisterType<ActiveUrls>().As<IActiveUrls>().SingleInstance();
            builder.RegisterType<ApiAppVersion>().As<IApiAppVersion>().SingleInstance();
            builder.RegisterType<OsEnvironment>().As<IEnvironment>().SingleInstance();
            builder.RegisterType<SystemProcesses>().As<IOsProcesses>().SingleInstance();
            builder.Register(c => 
                    new SafeSystemNetworkInterfaces(c.Resolve<ILogger>(), new SystemNetworkInterfaces()))
                .As<INetworkInterfaces>().SingleInstance();
            builder.RegisterType<NetworkInterfaceLoader>().As<INetworkInterfaceLoader>().SingleInstance();
            builder.RegisterType<HttpClients>().As<IHttpClients>().SingleInstance();
            builder.Register(c => Schedulers.FromApplicationDispatcher()).As<IScheduler>().SingleInstance();
            builder.Register(c => new TokenStorage(c.Resolve<UserSettings>())).As<ITokenStorage>().SingleInstance();

            builder.Register(c =>
                new CertificateHandler(
                    c.Resolve<Common.Configuration.Config>().TlsPinningConfig,
                    c.Resolve<IReportClient>())).SingleInstance();

            builder.Register(c => 
                new SafeDnsHandler(
                        c.Resolve<IEventAggregator>(),
                        c.Resolve<IDnsClient>())
                    { InnerHandler = c.Resolve<CertificateHandler>() }).SingleInstance();

            builder.Register(c =>
                new LoggingHandler(
                        c.Resolve<ILogger>())
                    {InnerHandler = c.Resolve<SafeDnsHandler>()});

            builder.Register(c =>
                new RetryingHandler(
                        c.Resolve<Common.Configuration.Config>().ApiTimeout,
                        c.Resolve<Common.Configuration.Config>().ApiRetries,
                        (retryCount, response, context) => new SleepDurationProvider(response).Value())
                    {InnerHandler = c.Resolve<LoggingHandler>()}).SingleInstance();

            builder.Register(c =>
                new OutdatedAppHandler
                    {InnerHandler = c.Resolve<RetryingHandler>()}).SingleInstance();

            builder.Register(c =>
                new HumanVerificationHandler(c.Resolve<IHumanVerifier>(), WebViewConfig.IsWebViewSupported())
                    {InnerHandler = c.Resolve<OutdatedAppHandler>()}).SingleInstance();

            builder.Register(c =>
                new UnauthorizedResponseHandler(
                        c.Resolve<ITokenClient>(),
                        c.Resolve<ITokenStorage>(),
                        c.Resolve<IUserStorage>(),
                        c.Resolve<ILogger>())
                    {InnerHandler = c.Resolve<HumanVerificationHandler>()}).SingleInstance();

            builder.Register(c =>
                new CancellingHandler
                    {InnerHandler = c.Resolve<UnauthorizedResponseHandler>()}).SingleInstance();

            builder.Register(c =>
                new AlternativeHostHandler(c.Resolve<DohClients>(),
                        c.Resolve<MainHostname>(),
                        c.Resolve<IAppSettings>(),
                        c.Resolve<GuestHoleState>(),
                        new Uri(c.Resolve<Common.Configuration.Config>().Urls.ApiUrl).Host)
                    { InnerHandler = c.Resolve<CancellingHandler>() }).SingleInstance();

            builder.Register(c =>
                    new TokenClient(
                        c.Resolve<ILogger>(),
                        new HttpClient(c.Resolve<RetryingHandler>())
                            {BaseAddress = c.Resolve<IActiveUrls>().ApiUrl.Uri},
                        c.Resolve<IApiAppVersion>(),
                        c.Resolve<ITokenStorage>(),
                        c.Resolve<Common.Configuration.Config>().ApiVersion,
                        TranslationSource.Instance.CurrentCulture.Name))
                .As<ITokenClient>()
                .SingleInstance();

            builder.Register(c =>
                {
                    var certificateHandler = new CertificateHandler(
                        c.Resolve<Common.Configuration.Config>().TlsPinningConfig,
                        c.Resolve<IReportClient>());

                    var safeDnsHandler = new SafeDnsHandler(
                            c.Resolve<IEventAggregator>(),
                            c.Resolve<IDnsClient>())
                        {InnerHandler = certificateHandler};

                    var loggingHandler = new LoggingHandler(
                            c.Resolve<ILogger>())
                        {InnerHandler = safeDnsHandler};

                    var retryingHandler = new RetryingHandler(
                            c.Resolve<Common.Configuration.Config>().ApiTimeout,
                            c.Resolve<Common.Configuration.Config>().ApiRetries,
                            (retryCount, response, context) => new SleepDurationProvider(response).Value())
                        {InnerHandler = loggingHandler};

                    var alternativeHostHandler = new AlternativeHostHandler(
                        c.Resolve<DohClients>(),
                        c.Resolve<MainHostname>(),
                        c.Resolve<IAppSettings>(),
                        c.Resolve<GuestHoleState>(),
                        new Uri(c.Resolve<Common.Configuration.Config>().Urls.ApiUrl).Host)
                    { InnerHandler = retryingHandler};

                    return new ApiClient(
                        new HttpClient(c.Resolve<AlternativeHostHandler>())
                        {
                            BaseAddress = c.Resolve<IActiveUrls>().ApiUrl.Uri
                        },
                        new HttpClient(alternativeHostHandler)
                        {
                            BaseAddress = c.Resolve<IActiveUrls>().ApiUrl.Uri,
                            DefaultRequestHeaders = { ConnectionClose = true }
                        },
                        c.Resolve<ILogger>(),
                        c.Resolve<ITokenStorage>(),
                        c.Resolve<IApiAppVersion>(),
                        c.Resolve<Common.Configuration.Config>().ApiVersion,
                        TranslationSource.Instance.CurrentCulture.Name);
                })
                .As<IApiClient>()
                .SingleInstance();

            builder.RegisterType<ActionableFailureApiResultEventHandler>().SingleInstance();

            builder.Register(c => new NLogLoggingConfiguration(c.Resolve<Common.Configuration.Config>().AppLogFolder, "app"))
                .AsSelf().SingleInstance();
            builder.RegisterType<NLogLoggerFactory>().As<ILoggerFactory>().SingleInstance();
            builder.Register(c => c.Resolve<ILoggerFactory>().Logger())
                .As<ILogger>().SingleInstance();
            builder.RegisterType<LogCleaner>().SingleInstance();
            builder.RegisterType<UpdateService>().AsSelf().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<ServiceClient>().SingleInstance();
            builder.RegisterType<UpdateEvents>().SingleInstance();

            builder.RegisterType<UserValidator>().SingleInstance();

            builder.Register(c => new UserAuth(
                c.Resolve<IApiClient>(),
                c.Resolve<ILogger>(),
                c.Resolve<IUserStorage>(),
                c.Resolve<ITokenStorage>(),
                c.Resolve<IAuthCertificateManager>())).SingleInstance();

            builder.RegisterType<NetworkClient>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<DnsClients>().As<IDnsClients>().SingleInstance();
            builder.Register(c =>
                    new SafeDnsClient(
                        new DnsClient(
                            c.Resolve<IDnsClients>(),
                            c.Resolve<INetworkInterfaces>())))
                .As<IDnsClient>().SingleInstance();

            builder.Register(c =>
                    new CachingReportClient(
                        new ReportClient(c.Resolve<IActiveUrls>().TlsReportUrl.Uri)))
                .As<IReportClient>()
                .SingleInstance();

            builder.RegisterType<EventClient>().SingleInstance();
            builder.RegisterType<UserInfoHandler>().AsImplementedInterfaces().SingleInstance();

            builder.RegisterType<VpnProfileHandler>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<VpnCredentialProvider>().As<IVpnCredentialProvider>().SingleInstance();
            builder.Register(c => new EventTimer(
                    c.Resolve<EventClient>(),
                    c.Resolve<Common.Configuration.Config>().EventCheckInterval.RandomizedWithDeviation(0.2))) 
                .AsSelf()
                .AsImplementedInterfaces()
                .SingleInstance();
            builder.Register(c => new SafeSystemProxy(c.Resolve<ILogger>(), new SystemProxy()))
                .AsImplementedInterfaces()
                .SingleInstance();
            builder.Register(c => new MainHostname(c.Resolve<Common.Configuration.Config>().Urls.ApiUrl)).SingleInstance();
            builder.Register(c => new DohClients(
                c.Resolve<Common.Configuration.Config>().DoHProviders,
                c.Resolve<Common.Configuration.Config>().DohClientTimeout))
                .SingleInstance();
            builder.RegisterType<UnhandledExceptionLogging>().SingleInstance();
            builder.RegisterType<MainWindowState>().As<IMainWindowState>().SingleInstance();
            builder.RegisterType<SingleActionFactory>().As<ISingleActionFactory>().SingleInstance();
            builder.RegisterType<LastServerLoadTimeProvider>().As<ILastServerLoadTimeProvider>().SingleInstance();
            builder.RegisterType<ClientConfig>().AsImplementedInterfaces().SingleInstance();
            builder.Register(c => new AnnouncementService(
                c.Resolve<IAppSettings>(),
                c.Resolve<IScheduler>(),
                c.Resolve<IApiClient>(),
                c.Resolve<IAnnouncementCache>(),
                c.Resolve<Common.Configuration.Config>().AnnouncementUpdateInterval))
                .AsImplementedInterfaces()
                .SingleInstance();
            builder.RegisterType<AnnouncementCache>().As<IAnnouncementCache>().SingleInstance();
        }
    }
}