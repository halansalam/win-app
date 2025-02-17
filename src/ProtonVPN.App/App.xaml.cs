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
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Toolkit.Uwp.Notifications;
using ProtonVPN.Common.Cli;
using ProtonVPN.Common.Configuration;
using ProtonVPN.Common.CrashReporting;
using ProtonVPN.Common.Extensions;
using ProtonVPN.Config;
using ProtonVPN.Core;
using ProtonVPN.Core.Startup;
using ProtonVPN.Native.PInvoke;
using ProtonVPN.Notifications;

namespace ProtonVPN
{
    public partial class App
    {
        private static Bootstrapper _bootstrapper;

        private static bool _failedToLoadAssembly;

        [STAThread]
        public static void Main(string[] args)
        {
            Run(args).GetAwaiter().GetResult();
        }

        private static async Task Run(string[] args)
        {
            // The app v1.13.0 starts update installer under local SYSTEM account.
            // Therefore, when update is complete, the installer starts the app under
            // SYSTEM account too. The app running under local SYSTEM account
            // cannot access user settings. 
            //
            // If the app detects it is started under local SYSTEM account, it
            // tries to restart itself under current user account. 
            var shouldRestartAsUser = ElevatedApplication.RunningAsSystem();
            if (shouldRestartAsUser)
            {
                await Task.Delay(2000);
                ElevatedApplication.LaunchAsUser();
                return;
            }

            if (await SingleInstanceApplication.InitializeAsFirstInstance("{588dc704-8eac-4a43-9345-ec7186b23f05}", args))
            {
                AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyLoadFailed;

                SetDllDirectories();

                var config = GetConfig();

                InitCrashReporting(config);
                CreateProfileOptimization(config);

                var app = new App();
                app.InitializeComponent();

                _bootstrapper = new Bootstrapper(args);
                _bootstrapper.Initialize();

                HandleIntentionalCrash(app, args);

                app.Run();
            }
        }

        private static void InitCrashReporting(Common.Configuration.Config config)
        {
            CrashReports.Init(config);
        }

        private static Assembly OnAssemblyLoadFailed(object sender, ResolveEventArgs args)
        {
            if (_failedToLoadAssembly)
            {
                return null;
            }

            var name = new AssemblyName(args.Name).Name;
            if (name.ContainsIgnoringCase(".resources") ||
                name.EndsWithIgnoringCase("XmlSerializers") ||
                name.StartsWithIgnoringCase("PresentationFramework.")
                )
            {
                return null;
            }

#if DEBUG
            if (name.StartsWithIgnoringCase("System.Windows"))
            {
                return null;
            }
#endif

            _failedToLoadAssembly = true;

            Process.Start("ProtonVPN.ErrorMessage.exe");
            Environment.Exit(0);

            return null;
        }

        private static void CreateProfileOptimization(Common.Configuration.Config config)
        {
            ProfileOptimization.SetProfileRoot(config.LocalAppDataFolder);
            ProfileOptimization.StartProfile("Startup.profile");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _bootstrapper.OnExit();
            base.OnExit(e);
        }

        private static Common.Configuration.Config GetConfig()
        {
#if DEBUG
            const bool savingAllowed = true;
#else
            const bool savingAllowed = false;
#endif
            var config = new ConfigFactory().Config(savingAllowed);
            new ConfigDirectories(config).Prepare();

            return config;
        }

        private static void HandleIntentionalCrash(Application app, string[] args)
        {
            var option = new CommandLineOption("crash", args);
            if (!option.Exists())
                return;

            app.Deactivated += (sender, ea) => throw new StackOverflowException("Intentional crash test");
        }

        private static void SetDllDirectories()
        {
            Kernel32.SetDefaultDllDirectories(Kernel32.SetDefaultDllDirectoriesFlags.LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
        }

        protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
        {
            base.OnSessionEnding(e);
            _bootstrapper.OnExit();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            AddToastNotificationActionHandler();
            base.OnStartup(e);
        }

        private void AddToastNotificationActionHandler()
        {
            ToastNotificationManagerCompat.OnActivated += OnToastNotificationUserAction;
        }

        private void OnToastNotificationUserAction(ToastNotificationActivatedEventArgsCompat e)
        {
            ToastArguments args = ToastArguments.Parse(e.Argument);
            Application.Current.Dispatcher.Invoke(delegate
            {
                NotificationUserAction data = new()
                {
                    Arguments = args.ToDictionary(p => p.Key, p => p.Value),
                    UserInputs = e.UserInput.ToDictionary(p => p.Key, p => p.Value)
                };
                _bootstrapper.OnToastNotificationUserAction(data);
            });
        }
    }
}