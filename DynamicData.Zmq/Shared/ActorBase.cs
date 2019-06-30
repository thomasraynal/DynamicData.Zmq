using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DynamicData.Zmq.Shared
{
    public abstract class ActorBase : IActor
    {
        public event OnActorDestroyed OnDestroyed;
        public event OnActorRunning OnRunning;

        public delegate void OnActorDestroyed();
        public delegate void OnActorRunning();

        private ILogger _logger;

        public ActorBase(ILogger logger)
        {
            Id = Guid.NewGuid();
            State = ActorState.Ready;
            _logger = logger;
        }

        public Guid Id { get; private set; }

        public ActorState State { get; private set; }

        public async Task Destroy()
        {
            if (State == ActorState.Destroyed) throw new InvalidOperationException("actor is already destroyed");

            await DestroyInternal();

            State = ActorState.Destroyed;

            OnDestroyed?.Invoke();

            _logger.LogInformation($"{this.GetType()} destroyed");
        }

        public async Task Run()
        {
            if (State == ActorState.Running) throw new InvalidOperationException("actor is already running");
            if (State == ActorState.Destroyed) throw new InvalidOperationException("actor has been destroyed");

            await RunInternal();

            State = ActorState.Running;

            OnRunning?.Invoke();

            _logger.LogInformation($"{this.GetType()} started");
        }

        protected async Task WaitForWorkProceduresToComplete(params Task[] tasks)
        {
            var isCompleted = tasks.All(task => task.IsCompleted);

            while (!isCompleted)
            {
                isCompleted = tasks.All(task => task.IsCompleted);

                await Task.Delay(100);
            }
        }

        protected abstract Task DestroyInternal();
        protected abstract Task RunInternal();

    }
}
