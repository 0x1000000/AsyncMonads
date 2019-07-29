using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace AsyncMonads
{
    public static class Reader
    {
        //Used to extract some value from a context
        public static Reader<TService> GetService<TService>() => 
            Reader<TService>.Read<IServiceProvider>(serviceProvider => (TService)serviceProvider.GetService(typeof(TService)));
    }

    [AsyncMethodBuilder(typeof(ReaderTaskMethodBuilder<>))]
    public class Reader<T> : INotifyCompletion, IReader
    {
        //Used by ReaderTaskMethodBuilder in a compiler generated code
        internal Reader() { }

        //Used to extract some value from a context
        public static Reader<T> Read<TCtx>(Func<TCtx, T> extractor) => new Reader<T>(ctx => Extract(ctx, extractor));

        private Reader(Func<object, T> exec) => this._extractor = exec;

        public bool IsCompleted { get; private set; }

        private readonly Func<object, T> _extractor;

        private object _ctx;

        private Action _continuation;

        private T _result;

        private Exception _exception;

        private IReader _child;

        public Reader<T> GetAwaiter() => this;

        public Reader<T> Apply(object ctx)
        {
            if (this._ctx != null)
            {
                throw new Exception("Another context is already applied to the reader");
            }
            this.SetCtx(ctx);
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
            if (this._ctx != null)
            {
                this._child.SetCtx(this._ctx);
            }
        }

        public void SetCtx(object ctx)
        {
            this._ctx = ctx;
            if (this._ctx != null)
            {
                this._child?.SetCtx(this._ctx);

                if (this._extractor != null)
                {
                    this.SetResult(this._extractor(this._ctx));
                }
            }
        }

        private static T Extract<TCtx>(object ctx, Func<TCtx, T> extractor)
        {
            if (extractor == null)
            {
                throw new Exception("Some extracting function should be defined");
            }
            if (ctx is TCtx tCtx)
            {
                return extractor(tCtx);
            }

            throw new Exception($"Could not cast the passed context to type '{typeof(TCtx).Name}'");
        }
    }

    public class ReaderTaskMethodBuilder<T>
    {
        public ReaderTaskMethodBuilder() => this.Task = new Reader<T>();

        public static ReaderTaskMethodBuilder<T> Create() => new ReaderTaskMethodBuilder<T>();

        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
            => stateMachine.MoveNext();

        public void SetStateMachine(IAsyncStateMachine stateMachine) { }

        public void SetException(Exception exception) => this.Task.SetException(exception);

        public void SetResult(T result) => this.Task.SetResult(result);

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

    internal interface IReader
    {
        void SetCtx(object ctx);
    }
}