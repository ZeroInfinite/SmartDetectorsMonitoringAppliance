﻿//-----------------------------------------------------------------------
// <copyright file="SmartDetectorRunnerMain.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace SmartDetectorRunnerChildProcess
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Monitoring.SmartDetectors.Arm;
    using Microsoft.Azure.Monitoring.SmartDetectors.Loader;
    using Microsoft.Azure.Monitoring.SmartDetectors.MonitoringAppliance;
    using Microsoft.Azure.Monitoring.SmartDetectors.MonitoringAppliance.Analysis;
    using Microsoft.Azure.Monitoring.SmartDetectors.MonitoringAppliance.ChildProcess;
    using Microsoft.Azure.Monitoring.SmartDetectors.MonitoringAppliance.Exceptions;
    using Microsoft.Azure.Monitoring.SmartDetectors.RuntimeEnvironment.Contracts;
    using Microsoft.Azure.Monitoring.SmartDetectors.Trace;
    using Unity;
    using ContractsAlert = Microsoft.Azure.Monitoring.SmartDetectors.RuntimeEnvironment.Contracts.Alert;

    /// <summary>
    /// The main class of the process that runs the Smart Detector
    /// </summary>
    public static class SmartDetectorRunnerMain
    {
        private static IUnityContainer container;

        /// <summary>
        /// The main method
        /// </summary>
        /// <param name="args">Command line arguments. These arguments are expected to be created by <see cref="IChildProcessManager.RunChildProcessAsync{TOutput}"/>.</param>
        /// <returns>Exit code</returns>
        public static int Main(string[] args)
        {
            IExtendedTracer tracer = null;
            try
            {
                // Inject dependencies
                container = DependenciesInjector.GetContainer()
                    .InjectAnalysisDependencies(withChildProcessRunner: false)
                    .WithChildProcessRegistrations(args);

                // Trace
                tracer = container.Resolve<IExtendedTracer>();
                tracer.TraceInformation($"Starting Smart Detector runner process, process ID {Process.GetCurrentProcess().Id}");

                // Run the analysis
                IChildProcessManager childProcessManager = container.Resolve<IChildProcessManager>();
                return childProcessManager.RunAndListenToParentAsync<SmartDetectorExecutionRequest, List<ContractsAlert>>(args, RunSmartDetectorAsync, ConvertExceptionToExitCode).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                tracer?.ReportException(e);
                tracer?.TraceError("Unhandled exception in child process: " + e.Message);
                Console.Error.WriteLine(e.ToString());
                return -1;
            }
        }

        /// <summary>
        /// Run the Smart Detector, by delegating the call to the registered <see cref="ISmartDetectorRunner"/>
        /// </summary>
        /// <param name="request">The Smart Detector request</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A <see cref="Task{TResult}"/>, returning the alerts presentations generated by the Smart Detector</returns>
        private static async Task<List<ContractsAlert>> RunSmartDetectorAsync(SmartDetectorExecutionRequest request, CancellationToken cancellationToken)
        {
            ISmartDetectorRunner smartDetectorRunner = container.Resolve<ISmartDetectorRunner>();
            bool shouldDetectorTrace = bool.Parse(ConfigurationReader.ReadConfig("ShouldDetectorTrace", required: true));
            return await smartDetectorRunner.RunAsync(request, shouldDetectorTrace, cancellationToken);
        }

        /// <summary>
        /// Converts an exception that was thrown from running a Smart Detector to the process's exit code
        /// </summary>
        /// <param name="e">The exception to convert</param>
        /// <returns>The process's exit code</returns>
        private static int ConvertExceptionToExitCode(Exception e)
        {
            switch (e)
            {
                case SmartDetectorNotFoundException _:
                    return (int)HttpStatusCode.NotFound;

                case SmartDetectorLoadException _:
                    return (int)HttpStatusCode.NotFound;

                case IncompatibleResourceTypesException _:
                    return (int)HttpStatusCode.BadRequest;

                case AzureResourceManagerClientException armce when armce.StatusCode.HasValue:
                    return (int)armce.StatusCode;

                case UnidentifiedAlertResourceTypeException _:
                    return (int)HttpStatusCode.BadRequest;

                default:
                    return (int)HttpStatusCode.InternalServerError;
            }
        }
    }
}
