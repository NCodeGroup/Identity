#region Copyright Preamble

// 
//    Copyright @ 2021 NCode Group
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

using System;
using System.Diagnostics.CodeAnalysis;
using GreenPipes;
using GreenPipes.Contracts;
using GreenPipes.Filters;
using NIdentity.OpenId.Playground.Contexts;

namespace NIdentity.OpenId.Playground
{
    internal class CompositePipeContextConverterFactory : IPipeContextConverterFactory<PipeContext>
    {
        private readonly IPipeContextConverterFactory<PipeContext>? _other;

        public CompositePipeContextConverterFactory()
        {
            // nothing
        }

        public CompositePipeContextConverterFactory(IPipeContextConverterFactory<PipeContext> other)
        {
            _other = other;
        }

        private static bool TryGetGenericTypeParameters(Type type, Type genericTypeDefinition, [NotNullWhen(true)] out Type[]? genericTypeParameters)
        {
            if (type.IsConstructedGenericType && type.GetGenericTypeDefinition() == genericTypeDefinition)
            {
                genericTypeParameters = type.GetGenericArguments();
                return true;
            }

            foreach (var interfaceType in type.GetInterfaces())
            {
                if (TryGetGenericTypeParameters(interfaceType, genericTypeDefinition, out genericTypeParameters))
                    return true;
            }

            genericTypeParameters = null;
            return false;
        }

        private static IPipeContextConverter<PipeContext, TOutput> CreateConverter<TOutput>(Type type, Type[] genericTypeParameters)
            where TOutput : class, PipeContext
        {
            return (IPipeContextConverter<PipeContext, TOutput>)Activator.CreateInstance(
                type.MakeGenericType(genericTypeParameters))!;
        }

        public IPipeContextConverter<PipeContext, TOutput> GetConverter<TOutput>()
            where TOutput : class, PipeContext
        {
            var typeOfOutput = typeof(TOutput);

            if (TryGetGenericTypeParameters(typeOfOutput, typeof(CommandContext<>), out var genericTypeParameters))
                return CreateConverter<TOutput>(typeof(CommandContextConverter<>), genericTypeParameters);

            if (TryGetGenericTypeParameters(typeOfOutput, typeof(EventContext<>), out genericTypeParameters))
                return CreateConverter<TOutput>(typeof(EventContextConverter<>), genericTypeParameters);

            if (TryGetGenericTypeParameters(typeOfOutput, typeof(RequestContext<>), out genericTypeParameters))
                return CreateConverter<TOutput>(typeof(RequestContextConverter<>), genericTypeParameters);

            if (TryGetGenericTypeParameters(typeOfOutput, typeof(ResultContext<,>), out genericTypeParameters))
                return CreateConverter<TOutput>(typeof(ResultContextConverter<,>), genericTypeParameters);

            if (TryGetGenericTypeParameters(typeOfOutput, typeof(IConsumeContext<,>), out genericTypeParameters))
                return CreateConverter<TOutput>(typeof(ConsumeContextConverter<,>), genericTypeParameters);

            return _other?.GetConverter<TOutput>() ?? NotSupportedConverter<TOutput>.Instance;
        }

        private class NotSupportedConverter<TOutput> : IPipeContextConverter<PipeContext, TOutput>
            where TOutput : class, PipeContext
        {
            public static readonly NotSupportedConverter<TOutput> Instance = new();

            public bool TryConvert(PipeContext input, out TOutput output)
            {
                output = default!;
                return false;
            }
        }

        private class CommandContextConverter<T> : IPipeContextConverter<PipeContext, CommandContext<T>>
            where T : class
        {
            public bool TryConvert(PipeContext input, out CommandContext<T> output)
            {
                switch (input)
                {
                    case CommandContext<T> commandContext:
                        output = commandContext;
                        return true;

                    default:
                        output = default!;
                        return false;
                }
            }
        }

        private class EventContextConverter<T> : IPipeContextConverter<PipeContext, EventContext<T>>
            where T : class
        {
            public bool TryConvert(PipeContext input, out EventContext<T> output)
            {
                switch (input)
                {
                    case EventContext<T> eventContext:
                        output = eventContext;
                        return true;

                    default:
                        output = default!;
                        return false;
                }
            }
        }

        private class RequestContextConverter<TRequest> : IPipeContextConverter<PipeContext, RequestContext<TRequest>>
            where TRequest : class
        {
            public bool TryConvert(PipeContext input, out RequestContext<TRequest> output)
            {
                switch (input)
                {
                    case RequestContext<TRequest> requestContext:
                        output = requestContext;
                        return true;

                    default:
                        output = default!;
                        return false;
                }
            }
        }

        private class ResultContextConverter<TRequest, TResult> : IPipeContextConverter<PipeContext, ResultContext<TRequest, TResult>>
            where TRequest : class
            where TResult : class
        {
            public bool TryConvert(PipeContext input, out ResultContext<TRequest, TResult> output)
            {
                switch (input)
                {
                    case ResultContext<TRequest, TResult> resultContext:
                        output = resultContext;
                        return false;

                    default:
                        output = default!;
                        return false;
                }
            }
        }

        private class ConsumeContextConverter<TRequest, TResponse> : IPipeContextConverter<PipeContext, IConsumeContext<TRequest, TResponse>>
            where TRequest : class
            where TResponse : class
        {
            public bool TryConvert(PipeContext input, out IConsumeContext<TRequest, TResponse> output)
            {
                switch (input)
                {
                    case IConsumeContext<TRequest, TResponse> consumeContext:
                        output = consumeContext;
                        return false;

                    default:
                        output = default!;
                        return false;
                }
            }
        }

    }
}
