using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.CompilerServices.ConfiguredTaskAwaitable;

namespace DynamicData.Shared
{
    public abstract class ActorBase : IActor
    {
        public event OnActorDestroyed OnDestroyed;
        public event OnActorRunning OnRunning;

        public delegate void OnActorDestroyed();
        public delegate void OnActorRunning();

        public ActorBase()
        {
            Id = Guid.NewGuid();
            State = ActorState.Ready;
        }

        public Guid Id { get; private set; }

        public ActorState State { get; private set; }

        public async Task Destroy()
        {
            if (State == ActorState.Destroyed) throw new InvalidOperationException("actor is already destroyed");
           // if (State == ActorState.Ready) throw new InvalidOperationException("actor must first be started");

            await DestroyInternal();

            State = ActorState.Destroyed;

            OnDestroyed?.Invoke();
        }

        public async Task Run()
        {
            if (State == ActorState.Running) throw new InvalidOperationException("actor is already running");
            if (State == ActorState.Destroyed) throw new InvalidOperationException("actor has been destroyed");

            await RunInternal();

            State = ActorState.Running;

            OnRunning?.Invoke();
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
