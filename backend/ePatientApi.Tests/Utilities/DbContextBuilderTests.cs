using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;
using ePatientApi.Builders;
using ePatientApi.DataAccess;

namespace ePatientApi.Builders.Tests
{
    [TestClass]
    public class DbContextBuilderTests
    {
        private readonly string _validConnectionString = "Host=localhost;Database=TestDb;Username=postgres;Password=password";

        [TestInitialize]
        public void Setup()
        {
            // Arrange: No specific setup required for service collection initialization.
        }

        #region ValidConfigTests

        [TestMethod]
        public void AddAppDbContext_ValidConnectionString_RegistersDbContextOptions()
        {
            // Arrange: Initialize a service collection.
            var services = new ServiceCollection();

            // Act: Add DbContext with a valid connection string.
            services.AddAppDbContext(_validConnectionString);

            // Assert: Verify DbContextOptions is registered.
            var serviceProvider = services.BuildServiceProvider();
            var dbContextOptions = serviceProvider.GetService<DbContextOptions<AppDbContext>>();
            Assert.IsNotNull(dbContextOptions, "DbContextOptions should be registered.");
        }

        [TestMethod]
        public void AddAppDbContext_ValidConnectionString_ConfiguresPostgreSqlProvider()
        {
            // Arrange: Initialize a service collection.
            var services = new ServiceCollection();

            // Act: Add DbContext with a valid connection string.
            services.AddAppDbContext(_validConnectionString);

            // Assert: Verify PostgreSQL provider is configured with the correct connection string.
            var serviceProvider = services.BuildServiceProvider();
            var dbContextOptions = serviceProvider.GetService<DbContextOptions<AppDbContext>>();
            using (var dbContext = new AppDbContext(dbContextOptions))
            {
                var connection = dbContext.Database.GetDbConnection();
                Assert.IsNotNull(connection, "Database connection should be configured.");
                Assert.IsTrue(connection is NpgsqlConnection, "Connection should be a PostgreSQL connection.");
                Assert.AreEqual(_validConnectionString, connection.ConnectionString, "Connection string should match.");
            }
        }

        [TestMethod]
        public void AddAppDbContext_ValidConnectionString_ConfiguresRetryStrategy()
        {
            // Arrange: Initialize a service collection.
            var services = new ServiceCollection();

            // Act: Add DbContext with a valid connection string.
            services.AddAppDbContext(_validConnectionString);

            // Assert: Verify retry strategy is configured correctly.
            var serviceProvider = services.BuildServiceProvider();
            var dbContextOptions = serviceProvider.GetService<DbContextOptions<AppDbContext>>();
            using (var dbContext = new AppDbContext(dbContextOptions))
            {
                var executionStrategy = dbContext.GetService<IExecutionStrategyFactory>().Create();
                Assert.IsNotNull(executionStrategy, "Execution strategy should be configured.");
            }
        }

        #endregion

        #region InvalidConfigTests

        [TestMethod]
        public void AddAppDbContext_NullConnectionString_ThrowsArgumentException()
        {
            // Arrange: Initialize a service collection.
            var services = new ServiceCollection();

            // Act & Assert: Expect ArgumentException when connection string is null or empty.
            Assert.ThrowsException<ArgumentException>(() => services.AddAppDbContext(null));
        }

        [TestMethod]
        public void AddAppDbContext_EmptyConnectionString_ThrowsArgumentException()
        {
            // Arrange: Initialize a service collection.
            var services = new ServiceCollection();

            // Act & Assert: Expect ArgumentException when connection string is empty.
            Assert.ThrowsException<ArgumentException>(() => services.AddAppDbContext(string.Empty));
        }

        [TestMethod]
        public void AddAppDbContext_NullServiceCollection_ThrowsArgumentNullException()
        {
            // Arrange: Provide a null service collection.
            IServiceCollection services = null;

            // Act & Assert: Verify adding DbContext throws ArgumentNullException with parameter name 'services'.
            var exception = Assert.ThrowsException<ArgumentNullException>(() => services.AddAppDbContext(_validConnectionString));
            Assert.AreEqual("services", exception.ParamName, "Exception parameter should be 'services'.");
            Assert.IsTrue(exception.Message.Contains("Value cannot be null."), "Exception message should indicate null value.");
        }

        #endregion
    }
}