using ITMartin.Media.Application.Abstractions.Workflows;
using ITMartin.Media.Application.Workflows.Models;

namespace ITMartin.FileSorter.Worker;
    public sealed class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public Worker(
            ILogger<Worker> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Worker started");

            using var scope =
                _scopeFactory.CreateScope();

            var workflowRegistry =
                scope.ServiceProvider
                    .GetRequiredService<IWorkflowRegistry>();

            var workflowExecutor =
                scope.ServiceProvider
                    .GetRequiredService<IWorkflowExecutor>();

            var workflow =
                workflowRegistry.Resolve(
                    "Package1Workflow");

            var context =
                new WorkflowExecutionContext<Package1WorkflowState>
                {
                    WorkflowId = Guid.NewGuid(),
                    WorkflowName = workflow.Name,
                    State =
                        new Package1WorkflowState
                        {
                            RootPath = @"C:\MediaTest"
                        }
                };
            _logger.LogInformation(
                "Worker startup complete");
            await Task.Delay(
                Timeout.Infinite,
                stoppingToken);
        }
    }
