﻿//-----------------------------------------------------------------------
// <copyright file="EmulationStatusControl.xaml.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Microsoft.Azure.Monitoring.SmartDetectors.MonitoringApplianceEmulator.Controls
{
    using System.Windows.Controls;
    using Microsoft.Azure.Monitoring.SmartDetectors.MonitoringApplianceEmulator.ViewModels;
    using Unity;

    /// <summary>
    /// Interaction logic for EmulationStatusControl.xaml
    /// </summary>
    public partial class EmulationStatusControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EmulationStatusControl"/> class.
        /// </summary>
        public EmulationStatusControl()
        {
            this.InitializeComponent();
            this.DataContext = App.Container.Resolve<EmulationStatusControlViewModel>();
        }

        /// <summary>
        /// Automatically scrolls to the end of the Tracer Box. The event occurs when content changes in the Tracer Box.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        public void TracerBoxTextChanged(object sender, TextChangedEventArgs args)
        {
            ((TextBox)sender).ScrollToEnd();
        }
    }
}
