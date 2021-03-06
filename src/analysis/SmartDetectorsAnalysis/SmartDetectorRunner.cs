﻿//-----------------------------------------------------------------------
// <copyright file="SmartDetectorRunner.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Microsoft.Azure.Monitoring.SmartDetectors.MonitoringAppliance.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Monitoring.SmartDetectors;
    using Microsoft.Azure.Monitoring.SmartDetectors.Clients;
    using Microsoft.Azure.Monitoring.SmartDetectors.Loader;
    using Microsoft.Azure.Monitoring.SmartDetectors.MonitoringAppliance;
    using Microsoft.Azure.Monitoring.SmartDetectors.MonitoringAppliance.Exceptions;
    using Microsoft.Azure.Monitoring.SmartDetectors.MonitoringAppliance.Trace;
    using Microsoft.Azure.Monitoring.SmartDetectors.Package;
    using Microsoft.Azure.Monitoring.SmartDetectors.Presentation;
    using Microsoft.Azure.Monitoring.SmartDetectors.RuntimeEnvironment.Contracts;
    using Microsoft.Azure.Monitoring.SmartDetectors.State;
    using Microsoft.Azure.Monitoring.SmartDetectors.Tools;
    using Microsoft.Azure.Monitoring.SmartDetectors.Trace;
    using Alert = Microsoft.Azure.Monitoring.SmartDetectors.Alert;
    using ContractsAlert = Microsoft.Azure.Monitoring.SmartDetectors.RuntimeEnvironment.Contracts.Alert;
    using ResourceType = Microsoft.Azure.Monitoring.SmartDetectors.ResourceType;

    /// <summary>
    /// An implementation of <see cref="ISmartDetectorRunner"/>, that loads the Smart Detector and runs it
    /// </summary>
    public class SmartDetectorRunner : ISmartDetectorRunner
    {
        private readonly ISmartDetectorRepository smartDetectorRepository;
        private readonly ISmartDetectorLoader smartDetectorLoader;
        private readonly IInternalAnalysisServicesFactory analysisServicesFactory;
        private readonly IExtendedAzureResourceManagerClient azureResourceManagerClient;
        private readonly IQueryRunInfoProvider queryRunInfoProvider;
        private readonly IStateRepositoryFactory stateRepositoryFactory;
        private readonly IExtendedTracer tracer;

        /// <summary>
        /// Initializes a new instance of the <see cref="SmartDetectorRunner"/> class
        /// </summary>
        /// <param name="smartDetectorRepository">The Smart Detector repository</param>
        /// <param name="smartDetectorLoader">The Smart Detector loader</param>
        /// <param name="analysisServicesFactory">The analysis services factory</param>
        /// <param name="azureResourceManagerClient">The Azure Resource Manager client</param>
        /// <param name="queryRunInfoProvider">The query run information provider</param>
        /// <param name="stateRepositoryFactory">The state repository factory</param>
        /// <param name="tracer">The tracer</param>
        public SmartDetectorRunner(
            ISmartDetectorRepository smartDetectorRepository,
            ISmartDetectorLoader smartDetectorLoader,
            IInternalAnalysisServicesFactory analysisServicesFactory,
            IExtendedAzureResourceManagerClient azureResourceManagerClient,
            IQueryRunInfoProvider queryRunInfoProvider,
            IStateRepositoryFactory stateRepositoryFactory,
            IExtendedTracer tracer)
        {
            this.smartDetectorRepository = Diagnostics.EnsureArgumentNotNull(() => smartDetectorRepository);
            this.smartDetectorLoader = Diagnostics.EnsureArgumentNotNull(() => smartDetectorLoader);
            this.analysisServicesFactory = Diagnostics.EnsureArgumentNotNull(() => analysisServicesFactory);
            this.azureResourceManagerClient = Diagnostics.EnsureArgumentNotNull(() => azureResourceManagerClient);
            this.queryRunInfoProvider = Diagnostics.EnsureArgumentNotNull(() => queryRunInfoProvider);
            this.stateRepositoryFactory = Diagnostics.EnsureArgumentNotNull(() => stateRepositoryFactory);
            this.tracer = tracer;
        }

        #region Implementation of ISmartDetectorRunner

        /// <summary>
        /// Loads the Smart Detector, runs it, and returns the generated alert presentations
        /// </summary>
        /// <param name="request">The Smart Detector request</param>
        /// <param name="shouldDetectorTrace">Determines if the detector's traces are emitted</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A <see cref="Task{TResult}"/>, returning the list of Alerts presentations generated by the Smart Detector</returns>
        public async Task<List<ContractsAlert>> RunAsync(SmartDetectorExecutionRequest request, bool shouldDetectorTrace, CancellationToken cancellationToken)
        {
            // Read the Smart Detector's package
            this.tracer.TraceInformation($"Loading Smart Detector package for Smart Detector ID {request.SmartDetectorId}");
            SmartDetectorPackage smartDetectorPackage = await this.smartDetectorRepository.ReadSmartDetectorPackageAsync(request.SmartDetectorId, cancellationToken);
            SmartDetectorManifest smartDetectorManifest = smartDetectorPackage.Manifest;
            this.tracer.TraceInformation($"Read Smart Detector package, ID {smartDetectorManifest.Id}, Version {smartDetectorManifest.Version}");

            // Load the Smart Detector
            ISmartDetector smartDetector = this.smartDetectorLoader.LoadSmartDetector(smartDetectorPackage);
            this.tracer.TraceInformation($"Smart Detector instance loaded successfully, ID {smartDetectorManifest.Id}");

            // Get the resources on which to run the Smart Detector
            List<ResourceIdentifier> resources = await this.GetResourcesForSmartDetector(request.ResourceIds, smartDetectorManifest, cancellationToken);

            // Create state repository
            IStateRepository stateRepository = this.stateRepositoryFactory.Create(request.SmartDetectorId, request.AlertRuleResourceId);

            // Run the Smart Detector
            this.tracer.TraceInformation($"Started running Smart Detector ID {smartDetectorManifest.Id}, Name {smartDetectorManifest.Name}");
            List<Alert> alerts;
            try
            {
                var analysisRequest = new AnalysisRequest(resources, request.Cadence, request.AlertRuleResourceId, this.analysisServicesFactory, stateRepository);
                ITracer detectorTracer = shouldDetectorTrace ? this.tracer : new EmptyTracer();
                alerts = await smartDetector.AnalyzeResourcesAsync(analysisRequest, detectorTracer, cancellationToken);
                this.tracer.TraceInformation($"Completed running Smart Detector ID {smartDetectorManifest.Id}, Name {smartDetectorManifest.Name}, returning {alerts.Count} alerts");
            }
            catch (Exception e)
            {
                this.tracer.TraceError($"Failed running Smart Detector ID {smartDetectorManifest.Id}, Name {smartDetectorManifest.Name}: {e}");
                throw new FailedToRunSmartDetectorException($"Calling Smart Detector '{smartDetectorManifest.Name}' failed with exception of type {e.GetType()} and message: {e.Message}", e);
            }

            // Verify that each alert belongs to one of the types declared in the Smart Detector manifest
            foreach (Alert alert in alerts)
            {
                if (!smartDetectorManifest.SupportedResourceTypes.Contains(alert.ResourceIdentifier.ResourceType))
                {
                    throw new UnidentifiedAlertResourceTypeException(alert.ResourceIdentifier);
                }
            }

            // Trace the number of alerts of each type
            foreach (var alertType in alerts.GroupBy(x => x.GetType().Name))
            {
                this.tracer.TraceInformation($"Got {alertType.Count()} Alerts of type '{alertType.Key}'");
                this.tracer.ReportMetric("AlertType", alertType.Count(), new Dictionary<string, string>() { { "AlertType", alertType.Key } });
            }

            // Create results
            List<ContractsAlert> results = new List<ContractsAlert>();
            foreach (var alert in alerts)
            {
                QueryRunInfo queryRunInfo = await this.queryRunInfoProvider.GetQueryRunInfoAsync(new List<ResourceIdentifier>() { alert.ResourceIdentifier }, cancellationToken);
                results.Add(alert.CreateContractsAlert(request, smartDetectorManifest.Name, queryRunInfo, this.analysisServicesFactory.UsedLogAnalysisClient, this.analysisServicesFactory.UsedMetricClient));
            }

            this.tracer.TraceInformation($"Returning {results.Count} results");
            return results;
        }

        #endregion

        /// <summary>
        /// Verify that the request resource type is supported by the Smart Detector, and enumerate
        /// the resources that the Smart Detector should run on.
        /// </summary>
        /// <param name="requestResourceIds">The request resource Ids</param>
        /// <param name="smartDetectorManifest">The Smart Detector manifest</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A <see cref="Task{TResult}"/>, returning the resource identifiers that the Smart Detector should run on</returns>
        private async Task<List<ResourceIdentifier>> GetResourcesForSmartDetector(IList<string> requestResourceIds, SmartDetectorManifest smartDetectorManifest, CancellationToken cancellationToken)
        {
            HashSet<ResourceIdentifier> resourcesForSmartDetector = new HashSet<ResourceIdentifier>();
            foreach (string requestResourceId in requestResourceIds)
            {
                ResourceIdentifier requestResource = ResourceIdentifier.CreateFromResourceId(requestResourceId);

                if (smartDetectorManifest.SupportedResourceTypes.Contains(requestResource.ResourceType))
                {
                    // If the Smart Detector directly supports the requested resource type, then that's it
                    resourcesForSmartDetector.Add(requestResource);
                }
                else if (requestResource.ResourceType == ResourceType.Subscription && smartDetectorManifest.SupportedResourceTypes.Contains(ResourceType.ResourceGroup))
                {
                    // If the request is for a subscription, and the Smart Detector supports a resource group type, enumerate all resource groups in the requested subscription
                    IList<ResourceIdentifier> resourceGroups = await this.azureResourceManagerClient.GetAllResourceGroupsInSubscriptionAsync(requestResource.SubscriptionId, cancellationToken);
                    resourcesForSmartDetector.UnionWith(resourceGroups);
                    this.tracer.TraceInformation($"Added {resourceGroups.Count} resource groups found in subscription {requestResource.SubscriptionId}");
                }
                else if (requestResource.ResourceType == ResourceType.Subscription)
                {
                    // If the request is for a subscription, enumerate all the resources in the requested subscription that the Smart Detector supports
                    IList<ResourceIdentifier> resources = await this.azureResourceManagerClient.GetAllResourcesInSubscriptionAsync(requestResource.SubscriptionId, smartDetectorManifest.SupportedResourceTypes, cancellationToken);
                    resourcesForSmartDetector.UnionWith(resources);
                    this.tracer.TraceInformation($"Added {resources.Count} resources found in subscription {requestResource.SubscriptionId}");
                }
                else if (requestResource.ResourceType == ResourceType.ResourceGroup && smartDetectorManifest.SupportedResourceTypes.Any(type => type != ResourceType.Subscription))
                {
                    // If the request is for a resource group, and the Smart Detector supports resource types (other than subscription),
                    // enumerate all the resources in the requested resource group that the Smart Detector supports
                    IList<ResourceIdentifier> resources = await this.azureResourceManagerClient.GetAllResourcesInResourceGroupAsync(requestResource.SubscriptionId, requestResource.ResourceGroupName, smartDetectorManifest.SupportedResourceTypes, cancellationToken);
                    resourcesForSmartDetector.UnionWith(resources);
                    this.tracer.TraceInformation($"Added {resources.Count} resources found in the specified resource group in subscription {requestResource.SubscriptionId}");
                }
                else
                {
                    // The Smart Detector does not support the requested resource type
                    throw new IncompatibleResourceTypesException(requestResource.ResourceType, smartDetectorManifest);
                }
            }

            return resourcesForSmartDetector.ToList();
        }
    }
}