﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Linq;

namespace EasyRequestHandlers.Request
{
    public class RequestHandlerBuilder
    {
        private readonly IServiceCollection _services;

        private readonly RequestHandlerOptions _options;

        private readonly Type[] _assemblyMarkers;

        public RequestHandlerBuilder(IServiceCollection services, RequestHandlerOptions options, Type[] assemblyMarkers)
        {
            _services = services;

            _options = options;

            _assemblyMarkers = assemblyMarkers;
        }

        public RequestHandlerBuilder WithMediatorPattern()
        {
            _options.EnableMediatorPattern = true;

            return this;
        }

        public RequestHandlerBuilder RemoveHandlerInjection()
        {
            _options.EnableHandlerInjection = false;

            return this;
        }

        public RequestHandlerBuilder WithBehavior(Type openGenericBehavior)
        {
            if (!_options.EnableMediatorPattern)
            {
                return this;
            }

            if (!openGenericBehavior.IsGenericTypeDefinition)
                throw new ArgumentException("Type must be an open generic like typeof(MyBehaviour<,>)");

            var implementsInterface = openGenericBehavior.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPipelineBehaviour<,>));

            if (!implementsInterface)
                throw new ArgumentException($"Type {openGenericBehavior.Name} must implement IPipelineBehaviour<,>");

            _services.TryAddEnumerable(
                ServiceDescriptor.Transient(typeof(IPipelineBehaviour<,>), openGenericBehavior));

            return this;
        }

        public RequestHandlerBuilder WithBehaviors(params Type[] behaviorTypes)
        {
            if (!_options.EnableMediatorPattern)
            {
                return this;
            }

            foreach (var behaviorType in behaviorTypes)
            {
                if (!behaviorType.IsGenericTypeDefinition || behaviorType.GetGenericTypeDefinition() != behaviorType)
                {
                    throw new ArgumentException($"Behaviour type {behaviorType.Name} must be an open generic type, e.g. LoggingBehaviour<,>");
                }

                var implementsInterface = behaviorType.GetInterfaces()
                    .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPipelineBehaviour<,>));

                if (!implementsInterface)
                {
                    throw new ArgumentException($"Type {behaviorType.Name} must implement IPipelineBehaviour<,>");
                }

                _services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IPipelineBehaviour<,>), behaviorType));
            }

            return this;
        }

        public RequestHandlerBuilder WithRequestHooks()
        {
            _options.EnableRequestHooks = true;

            return this;
        }


        public IServiceCollection Build()
        {
            HandlersRegister.RegisterHandlers(_services, _options, _assemblyMarkers);

            return _services;
        }
    }

}