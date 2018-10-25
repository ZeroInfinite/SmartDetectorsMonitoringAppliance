﻿//-----------------------------------------------------------------------
// <copyright file="StringExtensions.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Microsoft.Azure.Monitoring.SmartDetectors.Extensions
{
    using System.Globalization;
    using System.Security.Cryptography;
    using System.Text;
    using Microsoft.CodeAnalysis.CSharp.Scripting;
    using Tools;

    /// <summary>
    /// Extension methods for string objects
    /// </summary>
    public static class StringExtensions
    {
        private static readonly ObjectPool<HashAlgorithm> HashAlgoPool = new ObjectPool<HashAlgorithm>(SHA256.Create);

        /// <summary>
        /// Gets the hash of the specified string
        /// </summary>
        /// <param name="s">The string</param>
        /// <returns>The 256 bit hash value, represented by a string of 64 characters</returns>
        public static string Hash(this string s)
        {
            // Get the hash bytes
            byte[] hashBytes;
            using (var hashAlgo = HashAlgoPool.LeaseItem())
            {
                hashBytes = hashAlgo.Item.ComputeHash(Encoding.UTF8.GetBytes(s));
            }

            // Convert to string
            StringBuilder hash = new StringBuilder();
            foreach (byte b in hashBytes)
            {
                hash.AppendFormat(CultureInfo.InvariantCulture, "{0:x2}", b);
            }

            return hash.ToString();
        }

        /// <summary>
        /// Assumes the string is an interpolated string definition, including property names
        /// from the specified object, and returns the evaluated string value.
        /// Examples:
        ///    "User {Name} with role {Role} created at {CreationDate:D}".AsInterpolatedString(user)
        ///      => "User john@contoso.com with role Administrator created at Monday, June 15, 2009
        ///    "Found {NumAffectedMachines} affected {(NumAffectedMachines == 1 ? \"machine\" : \"machines\")}"
        ///      => "Found 1 affected machine", "Found 3 affected machines"
        /// </summary>
        /// <param name="interpolatedStringDefinition">The interpolated string definition.</param>
        /// <param name="source">The object, whose properties can appear in the interpolated string definition. The object type definition must be public.</param>
        /// <returns>The evaluated result string</returns>
        public static string EvaluateInterpolatedString(string interpolatedStringDefinition, object source)
        {
            if (!interpolatedStringDefinition.Contains("{"))
            {
                return interpolatedStringDefinition;
            }

            string code = "$\"" + interpolatedStringDefinition + "\"";
            return CSharpScript.EvaluateAsync<string>(code, globals: source, globalsType: source.GetType()).Result;
        }
    }
}