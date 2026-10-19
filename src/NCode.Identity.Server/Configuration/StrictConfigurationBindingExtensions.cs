#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using Microsoft.Extensions.Configuration;

namespace NCode.Identity.Server.Configuration;

/// <summary>
/// Binds configuration strictly, so a key that maps to nothing stops the host from starting instead of being silently
/// ignored. A misspelled or misplaced key otherwise looks exactly like an absent one: the server starts on its defaults
/// and the operator has no signal that their setting never took effect.
/// </summary>
// See ADR-0137.
internal static class StrictConfigurationBindingExtensions
{
    extension(IConfigurationSection section)
    {
        /// <summary>
        /// Binds this section onto <paramref name="instance"/>, failing when a key at any depth does not map to a
        /// property of the bound type. An absent section binds nothing.
        /// </summary>
        /// <param name="instance">The object to bind the section onto.</param>
        /// <exception cref="InvalidOperationException">
        /// The section contains a key that maps to no property, or a value that cannot be converted to its property's
        /// type. The message names the section so the operator can find the offending key.
        /// </exception>
        public void BindStrictly(object instance)
        {
            ArgumentNullException.ThrowIfNull(section);
            ArgumentNullException.ThrowIfNull(instance);

            try
            {
                section.Bind(
                    instance,
                    binderOptions => binderOptions.ErrorOnUnknownConfiguration = true
                );
            }
            catch (InvalidOperationException exception)
            {
                throw new InvalidOperationException(
                    $"The configuration section '{section.Path}' does not match {instance.GetType().Name}: "
                        + exception.Message,
                    exception
                );
            }
        }
    }
}
