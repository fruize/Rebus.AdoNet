﻿using System;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Linq;
using Rebus.Configuration;

namespace Rebus.AdoNet
{
	/// <summary>
	/// Configuration extensions to allow for fluently configuring Rebus with AdoNet.
	/// </summary>
	public static class AdoNetExtensions
	{
		private static ConnectionStringSettings GetConnectionString(string connectionStringName)
		{
			return ConfigurationManager.ConnectionStrings[connectionStringName];
		}

		/// <summary>
		/// Configures Rebus to store subscriptions in AdoNet.
		/// </summary>
		public static AdoNetSubscriptionStorageFluentConfigurer StoreInAdoNet(this RebusSubscriptionsConfigurer configurer, string connectionStringName, string subscriptionsTableName, IAdoNetConnectionFactory factory = null)
		{
			var connString = GetConnectionString(connectionStringName);

			if (factory == null)
				factory = new AdoNetConnectionFactory(connString.ConnectionString, connString.ProviderName);

			var storage = new AdoNetSubscriptionStorage(factory, subscriptionsTableName);

			configurer.Use(storage);

			return storage;
		}

		/// <summary>
		/// Configures Rebus to store sagas in AdoNet.
		/// </summary>
		public static AdoNetSagaPersisterFluentConfigurer StoreInAdoNet(this RebusSagasConfigurer configurer, string connectionStringName, string sagaTable, string sagaIndexTable,
			bool useSagaLocking = false, IAdoNetConnectionFactory factory = null)
		{
			var connString = GetConnectionString(connectionStringName);

			if (factory == null)
				factory = new AdoNetConnectionFactory(connString.ConnectionString, connString.ProviderName);

			var manager = new AdoNetUnitOfWorkManager(factory);

			configurer.Backbone.ConfigureEvents(x => x.AddUnitOfWorkManager(manager));
			var persister = new AdoNetSagaPersister(manager, sagaTable, sagaIndexTable, useSagaLocking);

			configurer.Use(persister);

			return persister;
		}

		/// <summary>
		/// Configures Rebus to store timeouts in AdoNet.
		/// </summary>
		public static AdoNetTimeoutStorageFluentConfigurer StoreInAdoNet(this RebusTimeoutsConfigurer configurer, string connectionStringName, string timeoutsTableName, IAdoNetConnectionFactory factory = null)
		{
			var connString = GetConnectionString(connectionStringName);

			if (factory == null)
				factory = new AdoNetConnectionFactory(connString.ConnectionString, connString.ProviderName);

			var storage = new AdoNetTimeoutStorage(factory, timeoutsTableName);

			configurer.Use(storage);

			return storage;
		}

		/// <summary>
		/// Configures Rebus to store timeouts in AdoNet. 
		/// Use batchSize for limit the number of timeouts that you get from database.
		/// </summary>
		public static AdoNetTimeoutStorageFluentConfigurer StoreInAdoNet(this RebusTimeoutsConfigurer configurer, string connectionStringName, string timeoutsTableName, uint batchsize, IAdoNetConnectionFactory factory = null)
		{
			var connString = GetConnectionString(connectionStringName);

			if (factory == null)
				factory = new AdoNetConnectionFactory(connString.ConnectionString, connString.ProviderName);

			var storage = new AdoNetTimeoutStorage(factory, timeoutsTableName, batchsize);

			configurer.Use(storage);

			return storage;
		}
	}
}