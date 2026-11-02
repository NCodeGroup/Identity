// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;

namespace NCode.Registration;

/// <summary>
/// Binds configuration strictly, so a key that maps to nothing stops the host from starting instead of being silently
/// ignored. A misspelled or misplaced key otherwise looks exactly like an absent one: the host starts on its defaults
/// and the operator has no signal that their setting never took effect.
/// </summary>
// See ADR-1137.
[PublicAPI]
public static class StrictConfigurationBindingExtensions
{
    /// <param name="section">The <see cref="IConfigurationSection"/> to bind.</param>
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
