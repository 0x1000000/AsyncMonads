using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace AsyncMonads
{
    [AsyncMethodBuilder(typeof(ReaderTaskMethodBuilder<>))]
    public class Reader<T> : INotifyCompletion, IReader
    {
        private Reader(Func<object, T> exec) => this._body = exec;

        internal Reader() { }

        public static Reader<T> Read<TCfg>(Func<TCfg, T> exec) => new Reader<T>(cfg => exec((TCfg)cfg));

        public bool IsCompleted { get; private set; }

        private readonly Func<object, T> _body;

        private object _cfg;

        private Action _continuation;

        private T _result;

        private Exception _exception;

        private IReader _child;

        public Reader<T> GetAwaiter() => this;

        public Reader<T> ApplyCfg(object cfg)
        {
            this.SetCfg(cfg);
            return this;
        }

        public void OnCompleted(Action continuation)
        {
            if (this.IsCompleted)
            {
                continuation();
            }
            else
            {
                if (this._continuation != null)
                {
                    throw new Exception("Only a single async continuation is allowed");
                }
                this._continuation = continuation;
            }
        }

        public T GetResult()
        {
            if (this._exception != null)
            {
                ExceptionDispatchInfo.Throw(this._exception);
            }

            if (!this.IsCompleted)
            {
                throw new Exception("Not Completed");
            }

            return this._result;
        }

        internal void SetResult(T result)
        {
            this._result = result;
            this.IsCompleted = true;
            this._continuation?.Invoke();
        }

        internal void SetException(Exception exception)
        {
            this._exception = exception;
            this.IsCompleted = true;
            this._continuation?.Invoke();
        }

        internal void SetChild(IReader reader)
        {
            this._child = reader;
            if (this._cfg != null)
            {
                this._child.SetCfg(this._cfg);
            }
        }

        public void SetCfg(object cfg)
        {
            this._cfg = cfg;
            this.ProcessChild();
        }

        private void ProcessChild()
        {
            if (this._cfg != null)
            {
                this._child?.SetCfg(this._cfg);

                if (this._body != null)
                {
                    this.SetResult(this._body(this._cfg));
                }
            }
        }
    }

    public class ReaderTaskMethodBuilder<T>
    {
        public ReaderTaskMethodBuilder()
        {
            this.Task = new Reader<T>();
        }

        public static ReaderTaskMethodBuilder<T> Create() => new ReaderTaskMethodBuilder<T>();

        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
        {
            stateMachine.MoveNext();
        }

        public void SetStateMachine(IAsyncStateMachine stateMachine)
        {
        }

        public void SetException(Exception exception)
        {
            this.Task.SetException(exception);
        }

        public void SetResult(T result)
        {
            this.Task.SetResult(result);
        }

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        =>
            this.GenericAwaitOnCompleted(ref awaiter, ref stateMachine);

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter,
            ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        =>
            this.GenericAwaitOnCompleted(ref awaiter, ref stateMachine);

        public void GenericAwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {

            if (awaiter is IReader reader)
            {
                this.Task.SetChild(reader);
            }

            awaiter.OnCompleted(stateMachine.MoveNext);
        }

        public Reader<T> Task { get; }
    }

    public interface IReader
    {
        void SetCfg(object cfg);
    }
}