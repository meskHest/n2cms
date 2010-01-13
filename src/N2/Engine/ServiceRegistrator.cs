﻿using System;
using N2.Plugin;

namespace N2.Engine
{
    /// <summary>
    /// Registers service in the N2 inversion of container upon start.
    /// </summary>
    public class ServiceRegistrator : IAutoStart
    {
        ITypeFinder finder;
        IServiceContainer container;

        public ServiceRegistrator(ITypeFinder finder, IServiceContainer container)
        {
            this.finder = finder;
            this.container = container;
        }

        #region IStartable Members

        public virtual void Start()
        {
            foreach (Type type in finder.Find(typeof(object)))
            {
                var attributes = type.GetCustomAttributes(typeof(ServiceAttribute), false);
                foreach (ServiceAttribute attribute in attributes)
                {
                    Type serviceType = attribute.ServiceType ?? type;
                    container.AddComponent(attribute.Key ?? serviceType.FullName, serviceType, type);
                }
            }
        }

        public virtual void Stop()
        {
        }

        #endregion
    }

}