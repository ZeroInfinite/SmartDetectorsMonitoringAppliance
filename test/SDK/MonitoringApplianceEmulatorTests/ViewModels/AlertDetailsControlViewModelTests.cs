﻿//-----------------------------------------------------------------------
// <copyright file="AlertDetailsControlViewModelTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace MonitoringApplianceEmulatorTests.ViewModels
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Monitoring.SmartDetectors;
    using Microsoft.Azure.Monitoring.SmartDetectors.MonitoringApplianceEmulator.Models;
    using Microsoft.Azure.Monitoring.SmartDetectors.MonitoringApplianceEmulator.ViewModels;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Alert = Microsoft.Azure.Monitoring.SmartDetectors.Alert;
    using AlertState = Microsoft.Azure.Monitoring.SmartDetectors.AlertState;
    using ResourceType = Microsoft.Azure.Monitoring.SmartDetectors.ResourceType;

    [TestClass]
    public class AlertDetailsControlViewModelTests
    {
        private readonly ResourceIdentifier virtualMachineResourceIdentifier = new ResourceIdentifier(ResourceType.VirtualMachine, "someSubscription", "someGroup", "someVM");
        private readonly ResourceIdentifier appInsightsResourceIdentifier = new ResourceIdentifier(ResourceType.ApplicationInsights, "someSubscription", "someGroup", "someApp");

        private Mock<ISystemProcessClient> systemProcessClientMock;

        [TestInitialize]
        public void Setup()
        {
            this.systemProcessClientMock = new Mock<ISystemProcessClient>();
        }

        [TestMethod]
        public void WhenCreatingNewViewModelThenItWasInitializedCorrectly()
        {
            EmulationAlert emulationAlert = EmulationAlertHelper.CreateEmulationAlert(new TestAlert(this.virtualMachineResourceIdentifier));
            bool wasCloseEventHandlerFired = false;

            var alertDetailsControlViewModel = new AlertDetailsControlViewModel(
                emulationAlert,
                () =>
                {
                    wasCloseEventHandlerFired = true;
                },
                this.systemProcessClientMock.Object);

            // Verify "Essentials" properties
            Assert.AreEqual("Subscription id", alertDetailsControlViewModel.EssentialsSectionProperties[0].ResourceType, "Unexpected essential property 'Subscription id'");
            Assert.AreEqual("someSubscription", alertDetailsControlViewModel.EssentialsSectionProperties[0].ResourceName, "Unexpected essential property 'Subscription id'");

            Assert.AreEqual("Resource group", alertDetailsControlViewModel.EssentialsSectionProperties[1].ResourceType, "Unexpected essential property 'Resource group'");
            Assert.AreEqual("someGroup", alertDetailsControlViewModel.EssentialsSectionProperties[1].ResourceName, "Unexpected essential property 'Resource group'");

            Assert.AreEqual("Resource type", alertDetailsControlViewModel.EssentialsSectionProperties[2].ResourceType, "Unexpected essential property 'Resource type'");
            Assert.AreEqual("VirtualMachine", alertDetailsControlViewModel.EssentialsSectionProperties[2].ResourceName, "Unexpected essential property 'Resource type'");

            Assert.AreEqual("Resource name", alertDetailsControlViewModel.EssentialsSectionProperties[3].ResourceType, "Unexpected essential property 'Resource name'");
            Assert.AreEqual("someVM", alertDetailsControlViewModel.EssentialsSectionProperties[3].ResourceName, "Unexpected essential property 'Resource name'");

            // Verify "Details" properties
            Assert.AreEqual(5, alertDetailsControlViewModel.DisplayableProperties.Count, "Unexpected count of displayable properties");
            for (var index = 0; index < alertDetailsControlViewModel.DisplayableProperties.Count - 1; index++)
            {
                string invalidOrderMessage =
                    $"Unexpected order of details section properties: Order of property in {index} index is {alertDetailsControlViewModel.DisplayableProperties[index].Order}, " +
                    $"while order of property in {index + 1} index is {alertDetailsControlViewModel.DisplayableProperties[index + 1].Order}";

                Assert.IsTrue(
                    alertDetailsControlViewModel.DisplayableProperties[index].Order <= alertDetailsControlViewModel.DisplayableProperties[index + 1].Order,
                    invalidOrderMessage);
            }

            // Verify close event was fired
            Assert.IsFalse(wasCloseEventHandlerFired);
            alertDetailsControlViewModel.CloseControlCommand.Execute(parameter: null);
            Assert.IsTrue(wasCloseEventHandlerFired);
        }

        [TestMethod]
        public void WhenExecutingOpenAnalyticsQueryCommandForNonAppInsightsResourceThenQueryWasExecutedAsExpected()
        {
            EmulationAlert emulationAlert = EmulationAlertHelper.CreateEmulationAlert(new TestAlert(this.virtualMachineResourceIdentifier));

            var alertDetailsControlViewModel = new AlertDetailsControlViewModel(
                emulationAlert,
                () => { },
                this.systemProcessClientMock.Object);

            alertDetailsControlViewModel.OpenAnalyticsQueryCommand.Execute(parameter: "<query>");

            string expectedAbsoluteUri = "https://portal.loganalytics.io/subscriptions/7904b7bd-5e6b-4415-99a8-355657b7da19/resourcegroups/MyResourceGroupName/workspaces/MyVirtualMachineName?q=H4sIAAAAAAAEALMpLE0tqrQDAJjF8mcHAAAA";

            // Verify that the query was composed and executed as expected
            this.systemProcessClientMock.Verify(m => m.StartWebBrowserProcess(It.Is<Uri>(u => u.AbsoluteUri == expectedAbsoluteUri)), Times.Once());
        }

        [TestMethod]
        public void WhenExecutingOpenAnalyticsQueryCommandForAppInsightsResourceThenQueryWasExecutedAsExpected()
        {
            EmulationAlert emulationAlert = EmulationAlertHelper.CreateEmulationAlert(new TestAlert(this.appInsightsResourceIdentifier));

            var alertDetailsControlViewModel = new AlertDetailsControlViewModel(
                emulationAlert,
                () => { },
                this.systemProcessClientMock.Object);

            alertDetailsControlViewModel.OpenAnalyticsQueryCommand.Execute(parameter: "<query>");

            string expectedAbsoluteUri = "https://analytics.applicationinsights.io/subscriptions/7904b7bd-5e6b-4415-99a8-355657b7da19/resourcegroups/MyResourceGroupName/components/someApp?q=H4sIAAAAAAAEALMpLE0tqrQDAJjF8mcHAAAA";

            // Verify that the query was composed and executed as expected
            this.systemProcessClientMock.Verify(m => m.StartWebBrowserProcess(It.Is<Uri>(u => u.AbsoluteUri == expectedAbsoluteUri)), Times.Once());
        }

        public class TestAlert : Alert
        {
            public TestAlert(ResourceIdentifier resourceIdentifier)
                : base("Test title", resourceIdentifier, AlertState.Active)
            {
                this.TextProperty1 = "Ahlan world";
                this.TextProperty2 = 5;
                this.KeyValue1 = new Dictionary<string, string>() { { "Donald", "Trump" }, { "Barak", "Obama" } };
                this.KeyValue2 = new Dictionary<string, string>() { { "Yaniv", "Katan" }, { "Avishai", "Zano" } };
                this.NoPresentation = "no show";
                this.TableProp = new List<TestTableAlertPropertyValue>()
                {
                    new TestTableAlertPropertyValue() { FirstName = "Edinson", LastName = "Cavani", Goals = 4.67 },
                    new TestTableAlertPropertyValue() { FirstName = "Fernando", LastName = "Torres", Goals = 1.7 }
                };
            }

            [AlertPresentationKeyValue("PresidentsKeyValue", "First name", "Last name", Order = 3)]
            public IDictionary<string, string> KeyValue1 { get; }

            [AlertPresentationKeyValue("PlayersKeyValue", Order = 4)]
            public IDictionary<string, string> KeyValue2 { get; }

            [AlertPresentationText("Some string", Order = 2)]
            public string TextProperty1 { get; }

            [AlertPresentationText("Some numeric string", Order = 1)]
            public int TextProperty2 { get; }

            public string NoPresentation { get; }

            [AlertPresentationTable("Some Table", Order = 5)]
            public List<TestTableAlertPropertyValue> TableProp { get; }
        }
    }
}
