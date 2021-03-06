﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Configuration;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CoreWCF.Configuration
{
    internal class ConfigurationManagerServiceModelOptions : IConfigureNamedOptions<ServiceModelOptions>, IDisposable
    {
        private readonly Lazy<ServiceModelSectionGroup> _section;
        private readonly WrappedConfigurationFile _file;

        private readonly IConfigurationHolder _holder;

        public ConfigurationManagerServiceModelOptions(IServiceProvider builder, string path)
        {
            _holder = builder.GetRequiredService<IConfigurationHolder>();
            _file = new WrappedConfigurationFile(path);

            _section = new Lazy<ServiceModelSectionGroup>(() =>
            {
                System.Configuration.Configuration configuration = ConfigurationManager.OpenMappedMachineConfiguration(new ConfigurationFileMap(_file.ConfigPath));
                var section = ServiceModelSectionGroup.GetSectionGroup(configuration);

                if (section is null)
                {
                    throw new ServiceModelConfigurationException("Section not found");
                }

                return section;
            }, true);
        }

        public void Dispose() => _file.Dispose();

        public void Configure(string name, ServiceModelOptions options)
        {
            Configure(options);
        }

        public void Configure(ServiceModelOptions options)
        {
            var configHolder = ParseConfig();
            foreach (var endpointName in configHolder.Endpoints.Keys)
            {
                IXmlConfigEndpoint configEndpoint = configHolder.GetXmlConfigEndpoint(endpointName);
                options.ConfigureService(configEndpoint.Service, serviceConfig =>
                {
                    serviceConfig.ConfigureServiceEndpoint(configEndpoint.Service, configEndpoint.Contract, configEndpoint.Binding, configEndpoint.Address, null);
                });
            }
        }

        private void ReadConfigSection(ServiceModelSectionGroup group)
        {
            if (group is null)
            {
                return;
            }

            AddBinding(group.Bindings?.BasicHttpBinding.Bindings);
            AddBinding(group.Bindings?.NetTcpBinding.Bindings);
            AddBinding(group.Bindings?.NetHttpBinding.Bindings);
            AddBinding(group.Bindings?.WSHttpBinding.Bindings);
            AddEndpoint(group.Services?.Services);
        }

        private void AddEndpoint(IEnumerable endpoints)
        {
            foreach (ServiceElement bindingElement in endpoints.OfType<ServiceElement>())
            {
                string serviceName = bindingElement.Name;

                foreach (ServiceEndpointElement endpoint in bindingElement.Endpoints.OfType<ServiceEndpointElement>())
                {
                    _holder.AddServiceEndpoint(
                        endpoint.Name,
                        serviceName,
                        endpoint.Address,
                        endpoint.Contract,
                        endpoint.Binding,
                        endpoint.BindingConfiguration);
                }
            }
        }

        private void AddBinding(IEnumerable bindings)
        {
            foreach (StandardBindingElement bindingElement in bindings.OfType<StandardBindingElement>())
            {
                Channels.Binding binding = bindingElement.CreateBinding();
                _holder.AddBinding(binding);
            }
        }

        private IConfigurationHolder ParseConfig()
        {
            ReadConfigSection(_section.Value);
            return _holder;
        }


    }
}
