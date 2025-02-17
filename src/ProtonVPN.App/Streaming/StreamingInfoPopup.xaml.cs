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

using System.Windows;

namespace ProtonVPN.Streaming
{
    public partial class StreamingInfoPopup
    {
        public static readonly DependencyProperty ShowPopupProperty = DependencyProperty.Register(
            "ShowPopup", typeof(bool), typeof(StreamingInfoPopup), new PropertyMetadata(false));

        public static readonly DependencyProperty PlacementTargetProperty = DependencyProperty.Register(
            "PlacementTarget", typeof(UIElement), typeof(StreamingInfoPopup), new PropertyMetadata(null));

        public StreamingInfoPopup()
        {
            InitializeComponent();
            CloseButton.Click += CloseButton_Click;
            Popup.MouseDown += (_, e) => e.Handled = true;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Popup.IsOpen = false;
        }

        public bool ShowPopup
        {
            get => (bool)GetValue(ShowPopupProperty);
            set => SetValue(ShowPopupProperty, value);
        }

        public UIElement PlacementTarget
        {
            get => (UIElement)GetValue(PlacementTargetProperty);
            set => SetValue(PlacementTargetProperty, value);
        }
    }
}