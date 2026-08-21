using Xunit;

// every test in this project shares one dev Postgres instance (docker-compose, port
// 55432) - no parallelization, or migrate-on-startup and shared rows race across tests
[assembly: CollectionBehavior(DisableTestParallelization = true)]
