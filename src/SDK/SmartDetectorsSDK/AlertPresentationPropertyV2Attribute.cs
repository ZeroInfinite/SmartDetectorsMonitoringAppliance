﻿//-----------------------------------------------------------------------
// <copyright file="AlertPresentationPropertyV2Attribute.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Microsoft.Azure.Monitoring.SmartDetectors
{
    using System;

    /// <summary>
    /// An attribute defining the presentation of a specific property in an <see cref="Alert"/>.
    /// The attribute determines the type of the presentation, the display name of the
    /// property and its order.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public abstract class AlertPresentationPropertyV2Attribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AlertPresentationPropertyV2Attribute"/> class.
        /// </summary>
        /// <param name="displayName">The display name to use when presenting the property's value</param>
        /// <exception cref="ArgumentNullException"><paramref name="displayName"/> is null or contains only white-spaces.</exception>
        protected AlertPresentationPropertyV2Attribute(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentNullException(nameof(displayName), "A property cannot be presented without a display name");
            }

            this.DisplayName = displayName;
        }

        /// <summary>
        /// Gets the display name of the property
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets or sets the order in which the property will be presented (optional)
        /// </summary>
        public byte Order { get; set; } = byte.MaxValue;

        /// <summary>
        /// Gets or sets the property name (optional)
        /// </summary>
        public string PropertyName { get; set; }
    }
}
