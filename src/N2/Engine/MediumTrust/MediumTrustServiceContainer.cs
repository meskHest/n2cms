using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using N2.Engine.Configuration;
using N2.Plugin;

namespace N2.Engine.MediumTrust
{
	public class MediumTrustServiceContainer : ServiceContainerBase
	{
		private readonly IDictionary<Type, object> container = new Dictionary<Type, object>();
		private readonly IDictionary<Type, Function<Type, object>> resolvers = new Dictionary<Type, Function<Type, object>>();

		public override IServiceContainerConfigurer ServiceContainerConfigurer
		{
			get { return new StaticDefaultServiceContainerConfigurer(); }
		}

		public override void AddFacility(string key, object facility)
		{
			throw new NotImplementedException();
		}

		public override void AddComponentLifeStyle(string key, Type serviceType, ComponentLifeStyle lifeStyle)
		{
			CheckForAutoStart(key, serviceType, serviceType);

			switch (lifeStyle)
			{
				case ComponentLifeStyle.Transient:
					RegisterTransientResolver(key, serviceType, serviceType);
					break;
				default:
					RegisterSingletonResolver(key, serviceType, serviceType);
					break;
			}
		}

		public override void AddComponentWithParameters(string key, Type serviceType, Type classType,
		                                                IDictionary<string, string> properties)
		{
			throw new NotImplementedException();
		}

		public override void AddComponentInstance(string key, Type serviceType, object instance)
		{
			if (resolvers.ContainsKey(serviceType))
				return;

			container[serviceType] = instance;
			resolvers[serviceType] = ReturnContainerInstance;

			if (instance is IAutoStart)
			{
				(instance as IAutoStart).Start();
			}
		}

		public override void AddComponent(string key, Type serviceType, Type classType)
		{
			CheckForAutoStart(key, serviceType, classType);

			RegisterSingletonResolver(key, serviceType, classType);
		}

		public override object Resolve(string key)
		{
			return Resolve(Type.GetType(key));
		}

		public override object Resolve(Type serviceType)
		{
			if (resolvers.ContainsKey(serviceType))
				return resolvers[serviceType](serviceType);

			if (serviceType.IsGenericType)
			{
				Type baseDefinition = serviceType.GetGenericTypeDefinition();

				if (resolvers.ContainsKey(baseDefinition))
				{
					return resolvers[baseDefinition](serviceType);
				}
			}

			throw new N2Exception("Couldn't find any service of the type " + serviceType);
		}

		public override void Release(object instance)
		{
			foreach (var pair in container)
			{
				if (pair.Value == instance)
				{
					container.Remove(pair.Key);
					break;
				}
			}
		}

		public override void StartComponents()
		{
			foreach (var resolver in resolvers)
			{
				CheckForAutoStart(resolver.Key.FullName.ToLowerInvariant(), resolver.Key, resolver.Key);
			}
		}

		private object ReturnContainerInstance(Type serviceType)
		{
			if (!container.ContainsKey(serviceType))
				throw new N2Exception("Couldn't find any service of the type " + serviceType);

			return container[serviceType];
		}

		private void CheckForAutoStart(string key, Type serviceType, Type classType)
		{
			foreach (Type t in classType.GetInterfaces())
			{
				if (t == typeof (IAutoStart))
					container[serviceType] = CreateInstance(serviceType, classType, key);
			}
		}

		private void RegisterSingletonResolver(string key, Type serviceType, Type classType)
		{
			resolvers[serviceType] = delegate(Type type)
			                         	{
			                         		if (container.ContainsKey(type))
			                         			return container[type];

			                         		object componentInstance = CreateInstance(type, classType, key);
			                         		container[type] = componentInstance;
			                         		return componentInstance;
			                         	};
		}

		private void RegisterTransientResolver(string key, Type serviceType, Type classType)
		{
			resolvers[serviceType] = delegate(Type type) { return CreateInstance(type, classType, key); };
		}

		protected object CreateInstance(Type serviceType, Type classType, string key)
		{
			if (classType.ContainsGenericParameters)
			{
				Type[] arguments = serviceType.GetGenericArguments();
				classType = classType.MakeGenericType(arguments);
			}

			ConstructorFindInfo constructorInfo = FindBestConstructor(classType);

			if (constructorInfo.ConstructorInfo == null)
			{
				var errorMessage = new StringBuilder("Could not find resolvable constructor for class " + classType.FullName);

				foreach (ParameterInfo parameter in constructorInfo.CouldNotFindParameters)
				{
					errorMessage.AppendLine("\nCould not resolve " + parameter.ParameterType);
				}

				throw new N2Exception(errorMessage.ToString());
			}
			ConstructorInfo constructor = constructorInfo.ConstructorInfo;
			
			object[] parameters = CreateConstructorParameters(constructor.GetParameters());
			object componentInstance = constructor.Invoke(parameters);
			AddComponentInstance(key, serviceType, componentInstance);
			return componentInstance;
		}

		protected virtual object[] CreateConstructorParameters(ParameterInfo[] parameterInfos)
		{
			var parameters = new object[parameterInfos.Length];
			for (int i = 0; i < parameterInfos.Length; i++)
			{
				parameters[i] = Resolve(parameterInfos[i].ParameterType);
			}
			return parameters;
		}

		protected virtual ConstructorFindInfo FindBestConstructor(Type classType)
		{
			int maxParameters = -1;
			ConstructorInfo bestConstructor = null;
			ParameterInfo[] couldNotFindParameters = null;
			foreach (ConstructorInfo constructor in classType.GetConstructors())
			{
				ParameterInfo[] parameters = constructor.GetParameters();
				ParameterInfo[] couldNotFindParametersTemp = ResolveAllParameters(parameters);
				if (parameters.Length <= maxParameters || couldNotFindParametersTemp.Length != 0)
				{
					couldNotFindParameters = couldNotFindParametersTemp;
					continue;
				}

				bestConstructor = constructor;
				maxParameters = parameters.Length;
				couldNotFindParameters = new ParameterInfo[0];
			}

			return new ConstructorFindInfo
			       	{
			       		ConstructorInfo = bestConstructor,
			       		CouldNotFindParameters = couldNotFindParameters
			       	};
		}

		private ParameterInfo[] ResolveAllParameters(IEnumerable<ParameterInfo> parameters)
		{
			var result = new List<ParameterInfo>();
			foreach (ParameterInfo parameter in parameters)
			{
				if (!resolvers.ContainsKey(parameter.ParameterType) &&
				    (!parameter.ParameterType.IsGenericType ||
				     !resolvers.ContainsKey(parameter.ParameterType.GetGenericTypeDefinition())))
				{
					result.Add(parameter);
				}
			}
			return result.ToArray();
		}

		#region Nested type: ConstructorFindInfo

		public class ConstructorFindInfo
		{
			public ConstructorInfo ConstructorInfo { get; set; }

			public ParameterInfo[] CouldNotFindParameters { get; set; }
		}

		#endregion
	}
}